namespace RiftVault.Models
{
    public enum AppMode
    {
        /// <summary>
        /// Standard full-featured Explorer with sidebar, multi-view lists, and preview/AI copilot drawers.
        /// </summary>
        Explorer,

        /// <summary>
        /// Split-pane Norton/Total Commander layout with dual active paths and bottom F3-F8 function key bar.
        /// </summary>
        DualCommander,

        /// <summary>
        /// Visual masonry grid tailored for photos and videos with zoom slider and lightbox overlay.
        /// </summary>
        MediaGallery,

        /// <summary>
        /// Developer workspace with Git repo & branch status, project badges, and bottom terminal runner.
        /// </summary>
        DeveloperWorkspace,

        /// <summary>
        /// Minimalist distraction-free canvas hiding sidebars and auxiliary panels.
        /// </summary>
        ZenFocus
    }
}
