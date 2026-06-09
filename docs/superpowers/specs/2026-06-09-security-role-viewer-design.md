# SecurityRoleViewer Plugin — Design Spec

## Overview

A read-only XrmToolBox plugin that displays Dataverse security roles and their privileges, organized in a tree view by role, with a detail panel showing all privileges and access levels for a selected entity. Supports text search, access-level filtering, and CSV export.

## Data Model & Queries

### Entities Queried

1. **`role`** — Security roles (name, roleid, businessunitid)
2. **`roleprivileges`** — Links roles to privileges with depth (access level)
3. **`privilege`** — Privilege metadata (name, accessright)
4. **`privilegeobjecttypecodes`** — Links privileges to entity type codes for grouping by entity

### Access Levels (Depth Values)

| Depth | Label           |
|-------|-----------------|
| 0     | None            |
| 1     | User            |
| 2     | Business Unit   |
| 4     | Parent-Child BU |
| 8     | Organization    |

### Loading Flow

1. User clicks "Load Roles"
2. `WorkAsync` fetches all roles for the connected organization
3. User selects a role from the tree
4. `WorkAsync` fetches that role's privileges joined with privilege metadata and entity type codes
5. Results cached in-memory — switching to a previously loaded role is instant

## UI Layout

### Structure

- **Toolbar** (top): Load Roles button, search textbox, access-level filter dropdown, Export button
- **SplitContainer** (center):
  - Left panel: `TreeView` showing role hierarchy
  - Right panel: `DataGridView` showing privileges for the selected entity
- **Status bar** (bottom): Uses `IStatusBarMessenger` to show loading state and current selection

### Tree Hierarchy

```
▸ Role Name (Business Unit)
  ▸ Core Entities
    Account
    Contact
    ...
  ▸ Custom Entities
    custom_foo
    ...
  ▸ Miscellaneous Privileges
    prvBulkDelete
    ...
```

- Top level: role name with business unit in parentheses
- Second level: categories (Core Entities, Custom Entities, Miscellaneous Privileges)
- Leaf level: entity or privilege name — selecting populates the detail grid

### Category Classification

- **Core Entities**: Object type code < 10000 (standard Dataverse entities)
- **Custom Entities**: Object type code >= 10000
- **Miscellaneous Privileges**: Privileges with no entity type code mapping (e.g., prvBulkDelete)

### Detail Grid

| Column         | Description                              |
|----------------|------------------------------------------|
| Privilege      | Privilege name (Create, Read, Write, etc.) |
| Access Level   | Human-readable depth label with color coding |

### Access Level Color Coding

| Level           | Color  |
|-----------------|--------|
| Organization    | Green  |
| Parent-Child BU | Blue   |
| Business Unit   | Yellow |
| User            | Orange |
| None            | Gray   |

### Search & Filter

- **Search box**: Filters tree nodes by entity name or role name (case-insensitive substring match)
- **Filter dropdown**: Options are "All", "Organization", "Parent-Child BU", "Business Unit", "User", "None" — filters tree to show only entities that have at least one privilege at the selected level

## Export

### Format: CSV

```
Role,Category,Entity,Privilege,AccessLevel
"Sales Manager","Core","Account","Create","Organization"
"Sales Manager","Core","Account","Read","Business Unit"
"Sales Manager","Misc","","prvBulkDelete","Organization"
```

- One row per privilege for the currently selected role
- Entity column empty for miscellaneous (non-entity-bound) privileges
- Access level as human-readable label
- `SaveFileDialog` with default filename `{RoleName}_Privileges.csv`

## Error Handling & Edge Cases

- **No connection**: Plugin requires a Dataverse connection. Does NOT implement `INoConnectionRequired`.
- **Large orgs**: In-memory cache prevents re-fetching. `WorkAsync` keeps UI responsive with progress messages via `SetWorkingMessage`.
- **Missing metadata**: Privileges with no entity type code mapping go into "Miscellaneous Privileges" category.
- **Duplicate role names**: Business unit shown in parentheses to disambiguate roles across BUs.

## Plugin Identity

- **Plugin name**: SecurityRoleViewer
- **Display name**: "Security Role Viewer"
- **Description**: "View security role privileges by entity with filtering and CSV export"
- **Base class**: `PluginControlBase`
- **Interfaces implemented**:
  - `IGitHubPlugin` — link to source repo for issue reporting
  - `IHelpPlugin` — link to documentation/help URL
  - `IStatusBarMessenger` — status bar messages during data loading

## Project Structure

```
Plugins/
  SecurityRoleViewer/
    SecurityRoleViewer.csproj
    Plugin.cs                  — MEF export, extends PluginBase
    SecurityRoleViewerControl.cs        — Main UI, extends PluginControlBase
    SecurityRoleViewerControl.Designer.cs — WinForms designer
    Models/
      RolePrivilegeInfo.cs     — Data model for a role's privilege entry
    Services/
      SecurityRoleService.cs   — Dataverse query logic (FetchXML/QueryExpression)
    Export/
      CsvExporter.cs           — CSV export logic
```

## Dependencies

- `XrmToolBox.Extensibility` (project reference)
- `System.ComponentModel.Composition` (MEF)
- `Microsoft.CrmSdk.CoreAssemblies` (IOrganizationService, QueryExpression)
- No additional NuGet packages required

## Out of Scope (v1)

- Editing/modifying security role permissions
- Side-by-side role comparison
- Excel export (CSV only)
- Persistent user settings/preferences
- Multi-connection support
