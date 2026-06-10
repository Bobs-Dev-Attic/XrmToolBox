using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SecurityRoleViewer
{
    /// <summary>
    /// Win32 interop for ListView features that the managed WinForms API does not
    /// expose on .NET Framework. The managed <c>ListViewGroup.CollapsedState</c>
    /// property only exists on .NET 5+, so collapsible groups need LVM_SETGROUPINFO.
    /// </summary>
    internal static class NativeMethods
    {
        private const int LVM_FIRST = 0x1000;
        private const int LVM_SETGROUPINFO = LVM_FIRST + 147;

        private const int LVGF_STATE = 0x00000004;
        private const int LVGS_COLLAPSIBLE = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        private struct LVGROUP
        {
            public int cbSize;
            public int mask;
            [MarshalAs(UnmanagedType.LPWStr)] public string pszHeader;
            public int cchHeader;
            [MarshalAs(UnmanagedType.LPWStr)] public string pszFooter;
            public int cchFooter;
            public int iGroupId;
            public int stateMask;
            public int state;
            public int uAlign;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LVGROUP lParam);

        /// <summary>
        /// Adds the native collapse/expand chevron to every group in the ListView.
        /// Safe to call repeatedly; a no-op if the handle isn't created yet or the
        /// internal group id can't be resolved (groups just stay non-collapsible).
        /// </summary>
        public static void MakeGroupsCollapsible(ListView listView)
        {
            if (listView == null || !listView.IsHandleCreated)
                return;

            foreach (ListViewGroup group in listView.Groups)
            {
                int groupId = GetGroupId(group);
                if (groupId < 0)
                    continue;

                var info = new LVGROUP
                {
                    cbSize = Marshal.SizeOf(typeof(LVGROUP)),
                    mask = LVGF_STATE,
                    stateMask = LVGS_COLLAPSIBLE,
                    state = LVGS_COLLAPSIBLE
                };

                SendMessage(listView.Handle, LVM_SETGROUPINFO, (IntPtr)groupId, ref info);
            }
        }

        // WinForms assigns each ListViewGroup an internal numeric id that the Win32
        // group messages key off of; it is not the collection index. The id is held
        // in a non-public property, so reflection is the supported way to read it.
        private static int GetGroupId(ListViewGroup group)
        {
            var prop = typeof(ListViewGroup).GetProperty(
                "ID", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.GetValue(group) is int id)
                return id;
            return -1;
        }
    }
}
