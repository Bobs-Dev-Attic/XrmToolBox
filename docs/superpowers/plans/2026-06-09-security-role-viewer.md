# SecurityRoleViewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a read-only XrmToolBox plugin that displays Dataverse security roles and their privileges in a tree view, with detail grid, search/filter, and CSV export.

**Architecture:** A WinForms plugin following XrmToolBox conventions: MEF-exported `PluginBase` subclass + `PluginControlBase` UserControl with SplitContainer layout. Data layer queries Dataverse for roles, privileges, and entity type codes, caches results in-memory. Export serializes to CSV via SaveFileDialog.

**Tech Stack:** .NET Framework 4.8, WinForms, MEF (System.ComponentModel.Composition), Microsoft.CrmSdk.CoreAssemblies (QueryExpression/FetchXML), XrmToolBox.Extensibility

---

## File Map

| File | Responsibility |
|------|---------------|
| `Plugins/SecurityRoleViewer/SecurityRoleViewer.csproj` | Project file targeting .NET 4.8, references Extensibility + CRM SDK |
| `Plugins/SecurityRoleViewer/Plugin.cs` | MEF export class extending PluginBase |
| `Plugins/SecurityRoleViewer/Models/RolePrivilegeInfo.cs` | Data model for a single privilege entry |
| `Plugins/SecurityRoleViewer/Services/SecurityRoleService.cs` | Dataverse query logic |
| `Plugins/SecurityRoleViewer/Export/CsvExporter.cs` | CSV export logic |
| `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.cs` | Main UI control (PluginControlBase) |
| `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.Designer.cs` | WinForms designer code |
| `Plugins/SecurityRoleViewer/Properties/AssemblyInfo.cs` | Assembly metadata |

---

### Task 1: Create project scaffolding and Plugin.cs

**Files:**
- Create: `Plugins/SecurityRoleViewer/SecurityRoleViewer.csproj`
- Create: `Plugins/SecurityRoleViewer/Plugin.cs`
- Create: `Plugins/SecurityRoleViewer/Properties/AssemblyInfo.cs`
- Modify: `XrmToolBox.sln`

- [ ] **Step 1: Create the .csproj file**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="12.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>SecurityRoleViewer</RootNamespace>
    <AssemblyName>SecurityRoleViewer</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <TargetFrameworkProfile />
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <Prefer32Bit>false</Prefer32Bit>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <Prefer32Bit>false</Prefer32Bit>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.ComponentModel.Composition" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.Data" />
    <Reference Include="System.Xml" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Plugin.cs" />
    <Compile Include="Models\RolePrivilegeInfo.cs" />
    <Compile Include="Services\SecurityRoleService.cs" />
    <Compile Include="Export\CsvExporter.cs" />
    <Compile Include="SecurityRoleViewerControl.cs">
      <SubType>UserControl</SubType>
    </Compile>
    <Compile Include="SecurityRoleViewerControl.Designer.cs">
      <DependentUpon>SecurityRoleViewerControl.cs</DependentUpon>
    </Compile>
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="SecurityRoleViewerControl.resx">
      <DependentUpon>SecurityRoleViewerControl.cs</DependentUpon>
    </EmbeddedResource>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\XrmToolBox.Extensibility\XrmToolBox.Extensibility.csproj">
      <Project>{df77aea3-43f7-403c-91af-3023a3bb06ec}</Project>
      <Name>XrmToolBox.Extensibility</Name>
    </ProjectReference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CrmSdk.CoreAssemblies">
      <Version>9.0.2.59</Version>
    </PackageReference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
  <PropertyGroup>
    <PostBuildEvent>xcopy /y "$(TargetPath)" "$(SolutionDir)XrmToolBox\$(OutDir)Plugins\"</PostBuildEvent>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create AssemblyInfo.cs**

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("SecurityRoleViewer")]
[assembly: AssemblyDescription("View security role privileges by entity with filtering and CSV export")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("SecurityRoleViewer")]
[assembly: AssemblyCopyright("")]
[assembly: ComVisible(false)]
[assembly: Guid("B7E3F1A2-4D5C-6E7F-8A9B-0C1D2E3F4A5B")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

- [ ] **Step 3: Create Plugin.cs**

```csharp
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    [Export(typeof(IXrmToolBoxPlugin)),
    ExportMetadata("Name", "Security Role Viewer"),
    ExportMetadata("Description", "View security role privileges by entity with filtering and CSV export"),
    ExportMetadata("SmallImageBase64", null),
    ExportMetadata("BigImageBase64", null),
    ExportMetadata("BackgroundColor", "#4A90D9"),
    ExportMetadata("PrimaryFontColor", "#FFFFFF"),
    ExportMetadata("SecondaryFontColor", "#E0E0E0")]
    public class Plugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new SecurityRoleViewerControl();
        }
    }
}
```

- [ ] **Step 4: Add the project to the solution under the Plugins folder**

Run:
```
dotnet sln XrmToolBox.sln add Plugins/SecurityRoleViewer/SecurityRoleViewer.csproj --solution-folder Plugins
```

If that fails (old-style .sln), manually add the project entry to `XrmToolBox.sln` under the existing `Plugins` solution folder GUID `{1A7814D2-1185-484E-92CC-7DD71DDE6488}`.

- [ ] **Step 5: Verify the project builds**

Run from the repo root:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Plugins/SecurityRoleViewer/SecurityRoleViewer.csproj Plugins/SecurityRoleViewer/Plugin.cs Plugins/SecurityRoleViewer/Properties/AssemblyInfo.cs XrmToolBox.sln
git commit -m "feat: scaffold SecurityRoleViewer plugin project with MEF export"
```

---

### Task 2: Create the RolePrivilegeInfo data model

**Files:**
- Create: `Plugins/SecurityRoleViewer/Models/RolePrivilegeInfo.cs`

- [ ] **Step 1: Create RolePrivilegeInfo.cs**

```csharp
namespace SecurityRoleViewer.Models
{
    public class RolePrivilegeInfo
    {
        public string RoleName { get; set; }
        public string EntityName { get; set; }
        public string PrivilegeName { get; set; }
        public int Depth { get; set; }
        public int ObjectTypeCode { get; set; }
        public string Category { get; set; }

        public string AccessLevelLabel
        {
            get
            {
                switch (Depth)
                {
                    case 0: return "None";
                    case 1: return "User";
                    case 2: return "Business Unit";
                    case 4: return "Parent-Child BU";
                    case 8: return "Organization";
                    default: return "Unknown";
                }
            }
        }
    }
}
```

- [ ] **Step 2: Verify it builds**

Run:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Plugins/SecurityRoleViewer/Models/RolePrivilegeInfo.cs
git commit -m "feat: add RolePrivilegeInfo data model with access level mapping"
```

---

### Task 3: Create the SecurityRoleService data layer

**Files:**
- Create: `Plugins/SecurityRoleViewer/Services/SecurityRoleService.cs`

- [ ] **Step 1: Create SecurityRoleService.cs**

This class encapsulates all Dataverse queries. It uses `QueryExpression` to fetch roles and their privileges.

```csharp
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SecurityRoleViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecurityRoleViewer.Services
{
    public class SecurityRoleService
    {
        private readonly IOrganizationService _service;
        private readonly Dictionary<Guid, List<RolePrivilegeInfo>> _cache
            = new Dictionary<Guid, List<RolePrivilegeInfo>>();

        public bool IsCached(Guid roleId) => _cache.ContainsKey(roleId);

        public SecurityRoleService(IOrganizationService service)
        {
            _service = service;
        }

        public List<Entity> GetRoles()
        {
            var query = new QueryExpression("role")
            {
                ColumnSet = new ColumnSet("name", "roleid", "businessunitid"),
                Orders = { new OrderExpression("name", OrderType.Ascending) }
            };

            var results = _service.RetrieveMultiple(query);
            return results.Entities.ToList();
        }

        public List<RolePrivilegeInfo> GetRolePrivileges(Guid roleId, string roleName)
        {
            if (_cache.TryGetValue(roleId, out var cached))
                return cached;

            var fetchXml = $@"
<fetch>
  <entity name='roleprivileges'>
    <attribute name='privilegedepthmask' />
    <filter>
      <condition attribute='roleid' operator='eq' value='{roleId}' />
    </filter>
    <link-entity name='privilege' from='privilegeid' to='privilegeid' alias='priv'>
      <attribute name='name' />
      <attribute name='accessright' />
      <link-entity name='privilegeobjecttypecodes' from='privilegeid' to='privilegeid' alias='otc' link-type='outer'>
        <attribute name='objecttypecode' />
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

            var results = _service.RetrieveMultiple(new FetchExpression(fetchXml));

            var privileges = new List<RolePrivilegeInfo>();
            foreach (var entity in results.Entities)
            {
                var privName = entity.GetAttributeValue<AliasedValue>("priv.name")?.Value?.ToString() ?? "";
                var depthMask = entity.GetAttributeValue<int>("privilegedepthmask");
                var otcValue = entity.GetAttributeValue<AliasedValue>("otc.objecttypecode")?.Value;

                int objectTypeCode = -1;
                string entityName = "";
                string category = "Miscellaneous";

                if (otcValue != null)
                {
                    objectTypeCode = Convert.ToInt32(otcValue);
                    entityName = otcValue.ToString();
                    category = objectTypeCode < 10000 ? "Core Entities" : "Custom Entities";
                }

                // privilegedepthmask: 1=User, 2=BU, 4=Parent-Child, 8=Org
                // but the depth value in roleprivileges is stored differently:
                // 0=None is not stored (absence means none)
                // The mask values map: 1->1(User), 2->2(BU), 4->4(ParentChild), 8->8(Org)
                int depth = 0;
                if (depthMask >= 8) depth = 8;
                else if (depthMask >= 4) depth = 4;
                else if (depthMask >= 2) depth = 2;
                else if (depthMask >= 1) depth = 1;

                privileges.Add(new RolePrivilegeInfo
                {
                    RoleName = roleName,
                    EntityName = entityName,
                    PrivilegeName = privName,
                    Depth = depth,
                    ObjectTypeCode = objectTypeCode,
                    Category = category
                });
            }

            // Filter out "none" entity type codes that are internal
            privileges = privileges
                .Where(p => p.ObjectTypeCode != 0 || p.Category == "Miscellaneous")
                .OrderBy(p => p.Category)
                .ThenBy(p => p.EntityName)
                .ThenBy(p => p.PrivilegeName)
                .ToList();

            _cache[roleId] = privileges;
            return privileges;
        }

        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
```

- [ ] **Step 2: Verify it builds**

Run:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Plugins/SecurityRoleViewer/Services/SecurityRoleService.cs
git commit -m "feat: add SecurityRoleService with role and privilege queries"
```

---

### Task 4: Create the CsvExporter

**Files:**
- Create: `Plugins/SecurityRoleViewer/Export/CsvExporter.cs`

- [ ] **Step 1: Create CsvExporter.cs**

```csharp
using SecurityRoleViewer.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SecurityRoleViewer.Export
{
    public static class CsvExporter
    {
        public static void Export(string filePath, List<RolePrivilegeInfo> privileges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Role,Category,Entity,Privilege,AccessLevel");

            foreach (var p in privileges)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                    Escape(p.RoleName),
                    Escape(p.Category),
                    Escape(p.EntityName),
                    Escape(p.PrivilegeName),
                    Escape(p.AccessLevelLabel)));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            return value.Replace("\"", "\"\"");
        }
    }
}
```

- [ ] **Step 2: Verify it builds**

Run:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Plugins/SecurityRoleViewer/Export/CsvExporter.cs
git commit -m "feat: add CsvExporter for role privilege data"
```

---

### Task 5: Create the SecurityRoleViewerControl Designer layout

**Files:**
- Create: `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.Designer.cs`
- Create: `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.resx`

- [ ] **Step 1: Create the Designer.cs file**

This defines the full WinForms layout: toolbar with Load/Search/Filter/Export, SplitContainer with TreeView (left) and DataGridView (right).

```csharp
namespace SecurityRoleViewer
{
    partial class SecurityRoleViewerControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tsbLoadRoles = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.tstSearch = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.tscbFilter = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbExport = new System.Windows.Forms.ToolStripButton();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tvRoles = new System.Windows.Forms.TreeView();
            this.dgvPrivileges = new System.Windows.Forms.DataGridView();
            this.colPrivilege = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAccessLevel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblDetailHeader = new System.Windows.Forms.Label();

            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrivileges)).BeginInit();
            this.SuspendLayout();

            // toolStrip1
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tsbLoadRoles,
                this.toolStripSeparator1,
                this.toolStripLabel1,
                this.tstSearch,
                this.toolStripSeparator2,
                this.toolStripLabel2,
                this.tscbFilter,
                this.toolStripSeparator3,
                this.tsbExport
            });
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(800, 25);

            // tsbLoadRoles
            this.tsbLoadRoles.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbLoadRoles.Name = "tsbLoadRoles";
            this.tsbLoadRoles.Text = "Load Roles";
            this.tsbLoadRoles.Click += new System.EventHandler(this.tsbLoadRoles_Click);

            // toolStripLabel1
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Text = "Search:";

            // tstSearch
            this.tstSearch.Name = "tstSearch";
            this.tstSearch.Size = new System.Drawing.Size(180, 25);
            this.tstSearch.TextChanged += new System.EventHandler(this.tstSearch_TextChanged);

            // toolStripLabel2
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Text = "Filter:";

            // tscbFilter
            this.tscbFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tscbFilter.Items.AddRange(new object[] {
                "All",
                "Organization",
                "Parent-Child BU",
                "Business Unit",
                "User",
                "None"
            });
            this.tscbFilter.Name = "tscbFilter";
            this.tscbFilter.Size = new System.Drawing.Size(130, 25);
            this.tscbFilter.SelectedIndexChanged += new System.EventHandler(this.tscbFilter_SelectedIndexChanged);

            // tsbExport
            this.tsbExport.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExport.Enabled = false;
            this.tsbExport.Name = "tsbExport";
            this.tsbExport.Text = "Export CSV";
            this.tsbExport.Click += new System.EventHandler(this.tsbExport_Click);

            // splitContainer1
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 25);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.SplitterDistance = 280;

            // tvRoles (Panel1)
            this.tvRoles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvRoles.Name = "tvRoles";
            this.tvRoles.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvRoles_AfterSelect);
            this.splitContainer1.Panel1.Controls.Add(this.tvRoles);

            // Panel2 layout
            this.lblDetailHeader = new System.Windows.Forms.Label();
            this.lblDetailHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDetailHeader.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblDetailHeader.Height = 30;
            this.lblDetailHeader.Name = "lblDetailHeader";
            this.lblDetailHeader.Text = "Select an entity from the tree";
            this.lblDetailHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblDetailHeader.Padding = new System.Windows.Forms.Padding(4, 0, 0, 0);

            // dgvPrivileges
            this.dgvPrivileges.AllowUserToAddRows = false;
            this.dgvPrivileges.AllowUserToDeleteRows = false;
            this.dgvPrivileges.ReadOnly = true;
            this.dgvPrivileges.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPrivileges.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrivileges.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colPrivilege,
                this.colAccessLevel
            });
            this.dgvPrivileges.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPrivileges.Name = "dgvPrivileges";
            this.dgvPrivileges.RowHeadersVisible = false;
            this.dgvPrivileges.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvPrivileges.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dgvPrivileges_CellFormatting);

            // colPrivilege
            this.colPrivilege.HeaderText = "Privilege";
            this.colPrivilege.Name = "colPrivilege";
            this.colPrivilege.FillWeight = 60;

            // colAccessLevel
            this.colAccessLevel.HeaderText = "Access Level";
            this.colAccessLevel.Name = "colAccessLevel";
            this.colAccessLevel.FillWeight = 40;

            this.splitContainer1.Panel2.Controls.Add(this.dgvPrivileges);
            this.splitContainer1.Panel2.Controls.Add(this.lblDetailHeader);

            // SecurityRoleViewerControl
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "SecurityRoleViewerControl";
            this.Size = new System.Drawing.Size(800, 500);

            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrivileges)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsbLoadRoles;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabel1;
        private System.Windows.Forms.ToolStripTextBox tstSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripLabel toolStripLabel2;
        private System.Windows.Forms.ToolStripComboBox tscbFilter;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton tsbExport;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView tvRoles;
        private System.Windows.Forms.DataGridView dgvPrivileges;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrivilege;
        private System.Windows.Forms.DataGridViewTextBoxColumn colAccessLevel;
        private System.Windows.Forms.Label lblDetailHeader;
    }
}
```

- [ ] **Step 2: Create the .resx file**

Create an empty resx file at `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.resx`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
</root>
```

- [ ] **Step 3: Verify it builds**

Run:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build will fail because `SecurityRoleViewerControl.cs` doesn't exist yet. That's expected at this step — the Designer references event handlers that Task 6 will implement. Proceed to Task 6.

- [ ] **Step 4: Commit**

```bash
git add Plugins/SecurityRoleViewer/SecurityRoleViewerControl.Designer.cs Plugins/SecurityRoleViewer/SecurityRoleViewerControl.resx
git commit -m "feat: add SecurityRoleViewerControl WinForms designer layout"
```

---

### Task 6: Implement SecurityRoleViewerControl (main UI logic)

**Files:**
- Create: `Plugins/SecurityRoleViewer/SecurityRoleViewerControl.cs`

- [ ] **Step 1: Create SecurityRoleViewerControl.cs**

This is the main plugin control that wires up all the behavior: loading roles, building the tree, populating the detail grid, handling search/filter, and triggering export.

```csharp
using Microsoft.Xrm.Sdk;
using SecurityRoleViewer.Export;
using SecurityRoleViewer.Models;
using SecurityRoleViewer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace SecurityRoleViewer
{
    public partial class SecurityRoleViewerControl : PluginControlBase, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger
    {
        private SecurityRoleService _roleService;
        private List<Entity> _allRoles;
        private Guid _selectedRoleId;
        private string _selectedRoleName;
        private List<RolePrivilegeInfo> _currentPrivileges;

        public SecurityRoleViewerControl()
        {
            InitializeComponent();
            tscbFilter.SelectedIndex = 0;
        }

        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public string RepositoryName => "XrmToolBox";
        public string UserName => "MscrmTools";
        public string HelpUrl => "https://github.com/MscrmTools/XrmToolBox";

        private void tsbLoadRoles_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadRoles);
        }

        private void LoadRoles()
        {
            _roleService = new SecurityRoleService(Service);

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading security roles...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRoles();
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Roles");
                        return;
                    }

                    _allRoles = (List<Entity>)args.Result;
                    BuildTree(_allRoles);
                    tsbExport.Enabled = false;

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_allRoles.Count} roles"));
                }
            });
        }

        private void BuildTree(List<Entity> roles)
        {
            tvRoles.BeginUpdate();
            tvRoles.Nodes.Clear();

            var searchText = tstSearch.Text?.Trim() ?? "";
            var filterLevel = tscbFilter.SelectedItem?.ToString() ?? "All";

            foreach (var role in roles)
            {
                var roleName = role.GetAttributeValue<string>("name") ?? "";
                var roleId = role.GetAttributeValue<Guid>("roleid");
                var buRef = role.GetAttributeValue<EntityReference>("businessunitid");
                var buName = buRef?.Name ?? "";

                var displayName = string.IsNullOrEmpty(buName)
                    ? roleName
                    : $"{roleName} ({buName})";

                // If we have cached privileges for this role, apply search/filter at tree level
                if (_roleService != null && HasCachedPrivileges(roleId))
                {
                    var privileges = _roleService.GetRolePrivileges(roleId, roleName);
                    var filtered = ApplyFilters(privileges, searchText, filterLevel);

                    if (!string.IsNullOrEmpty(searchText) && filtered.Count == 0
                        && !roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase).Equals(-1) == false
                        && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var roleNode = new TreeNode(displayName) { Tag = roleId };
                    AddCategoryNodes(roleNode, filtered);
                    tvRoles.Nodes.Add(roleNode);
                }
                else
                {
                    if (!string.IsNullOrEmpty(searchText)
                        && roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var roleNode = new TreeNode(displayName) { Tag = roleId };
                    tvRoles.Nodes.Add(roleNode);
                }
            }

            tvRoles.EndUpdate();
        }

        private bool HasCachedPrivileges(Guid roleId)
        {
            return _roleService.IsCached(roleId);
        }

        private void AddCategoryNodes(TreeNode roleNode, List<RolePrivilegeInfo> privileges)
        {
            var groups = privileges
                .GroupBy(p => p.Category)
                .OrderBy(g => g.Key == "Core Entities" ? 0 : g.Key == "Custom Entities" ? 1 : 2);

            foreach (var group in groups)
            {
                var categoryNode = new TreeNode(group.Key);

                var entityGroups = group.GroupBy(p => string.IsNullOrEmpty(p.EntityName) ? p.PrivilegeName : p.EntityName);
                foreach (var entityGroup in entityGroups.OrderBy(eg => eg.Key))
                {
                    var entityNode = new TreeNode(entityGroup.Key)
                    {
                        Tag = entityGroup.ToList()
                    };
                    categoryNode.Nodes.Add(entityNode);
                }

                roleNode.Nodes.Add(categoryNode);
            }
        }

        private List<RolePrivilegeInfo> ApplyFilters(List<RolePrivilegeInfo> privileges, string searchText, string filterLevel)
        {
            var result = privileges.AsEnumerable();

            if (!string.IsNullOrEmpty(searchText))
            {
                result = result.Where(p =>
                    p.EntityName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.PrivilegeName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (filterLevel != "All")
            {
                result = result.Where(p => p.AccessLevelLabel == filterLevel);
            }

            return result.ToList();
        }

        private void tvRoles_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is List<RolePrivilegeInfo> entityPrivileges)
            {
                PopulateGrid(entityPrivileges);
                lblDetailHeader.Text = e.Node.Text;
                return;
            }

            if (e.Node.Tag is Guid roleId)
            {
                _selectedRoleId = roleId;
                _selectedRoleName = e.Node.Text;
                tsbExport.Enabled = true;

                LoadRolePrivileges(roleId, e.Node);
            }
        }

        private void LoadRolePrivileges(Guid roleId, TreeNode roleNode)
        {
            if (_roleService == null) return;

            var roleName = roleNode.Text;

            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading privileges for {roleName}...",
                Work = (w, args) =>
                {
                    args.Result = _roleService.GetRolePrivileges(roleId, roleName);
                },
                PostWorkCallBack = args =>
                {
                    if (args.Error != null)
                    {
                        ShowErrorDialog(args.Error, "Loading Privileges");
                        return;
                    }

                    _currentPrivileges = (List<RolePrivilegeInfo>)args.Result;

                    var searchText = tstSearch.Text?.Trim() ?? "";
                    var filterLevel = tscbFilter.SelectedItem?.ToString() ?? "All";
                    var filtered = ApplyFilters(_currentPrivileges, searchText, filterLevel);

                    roleNode.Nodes.Clear();
                    AddCategoryNodes(roleNode, filtered);
                    roleNode.Expand();

                    tsbExport.Enabled = true;
                    dgvPrivileges.Rows.Clear();
                    lblDetailHeader.Text = $"{roleName} - {filtered.Count} privileges";

                    SendMessageToStatusBar?.Invoke(this,
                        new StatusBarMessageEventArgs($"Loaded {_currentPrivileges.Count} privileges for {roleName}"));
                }
            });
        }

        private void PopulateGrid(List<RolePrivilegeInfo> privileges)
        {
            dgvPrivileges.Rows.Clear();

            foreach (var p in privileges)
            {
                dgvPrivileges.Rows.Add(p.PrivilegeName, p.AccessLevelLabel);
            }
        }

        private void dgvPrivileges_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex != 1 || e.Value == null) return;

            var level = e.Value.ToString();
            switch (level)
            {
                case "Organization":
                    e.CellStyle.ForeColor = Color.DarkGreen;
                    e.CellStyle.Font = new Font(dgvPrivileges.Font, FontStyle.Bold);
                    break;
                case "Parent-Child BU":
                    e.CellStyle.ForeColor = Color.Blue;
                    break;
                case "Business Unit":
                    e.CellStyle.ForeColor = Color.DarkGoldenrod;
                    break;
                case "User":
                    e.CellStyle.ForeColor = Color.OrangeRed;
                    break;
                case "None":
                    e.CellStyle.ForeColor = Color.Gray;
                    break;
            }
        }

        private void tstSearch_TextChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                BuildTree(_allRoles);
        }

        private void tscbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_allRoles != null)
                BuildTree(_allRoles);
        }

        private void tsbExport_Click(object sender, EventArgs e)
        {
            if (_currentPrivileges == null || _currentPrivileges.Count == 0)
            {
                MessageBox.Show("No privileges loaded to export. Select a role first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                var safeName = string.Join("_", _selectedRoleName.Split(
                    System.IO.Path.GetInvalidFileNameChars()));
                dialog.FileName = $"{safeName}_Privileges.csv";
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.Title = "Export Role Privileges";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    CsvExporter.Export(dialog.FileName, _currentPrivileges);
                    MessageBox.Show($"Exported {_currentPrivileges.Count} privileges to:\n{dialog.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Verify full build**

Run:
```
msbuild Plugins\SecurityRoleViewer\SecurityRoleViewer.csproj /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Plugins/SecurityRoleViewer/SecurityRoleViewerControl.cs
git commit -m "feat: implement SecurityRoleViewerControl with tree view, grid, search, filter, and export"
```

---

### Task 7: Build the full solution and verify integration

**Files:**
- No new files

- [ ] **Step 1: Build the entire solution**

Run:
```
msbuild XrmToolBox.sln /t:Build /p:Configuration=Debug /verbosity:minimal
```

Expected: Build succeeded with 0 errors. The SecurityRoleViewer.dll should be copied to `XrmToolBox\bin\Debug\Plugins\` by the post-build event.

- [ ] **Step 2: Verify the DLL is in the Plugins output folder**

Run:
```powershell
Test-Path "XrmToolBox\bin\Debug\Plugins\SecurityRoleViewer.dll"
```

Expected: `True`

- [ ] **Step 3: Commit all remaining changes**

```bash
git add -A
git commit -m "feat: complete SecurityRoleViewer plugin - full solution build verified"
```
