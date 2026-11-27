using UnityEditor;
using UnityEditor.Toolbars;

namespace Mote.EditorTools
{
    internal static class MobileBuildToolbar
    {
        [MainToolbarElement("Mote/Mobile Build", defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static MainToolbarElement Create()
        {
            var content = new MainToolbarContent("모바일 빌드", "Mote 모바일 빌드 도구 열기");
            return new MainToolbarButton(content, () => MoteToolsWindow.OpenPage(typeof(MobileBuildPage)));
        }
    }
}
