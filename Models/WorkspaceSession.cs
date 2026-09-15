using System;
using System.Collections.Generic;

namespace RiftVault.Models
{
    public class WorkspaceSession
    {
        public string Name { get; set; } = string.Empty;
        public DateTime LastSaved { get; set; } = DateTime.Now;
        public List<TabSnapshot> Tabs { get; set; } = new();
        public int ActiveTabIndex { get; set; } = 0;
        public bool IsDualPane { get; set; } = false;
        public List<TabSnapshot> RightPaneTabs { get; set; } = new();
    }

    public class TabSnapshot
    {
        public string Path { get; set; } = string.Empty;
        public string ViewMode { get; set; } = "IconGrid";
        public List<string> History { get; set; } = new();
        public int HistoryIndex { get; set; } = -1;
    }
}