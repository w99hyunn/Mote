using UnityEngine.UIElements;

namespace Mote.EditorTools
{
    internal static class ToolSection
    {
        internal static VisualElement Add(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("tool-section");
            var heading = new Label(title);
            heading.AddToClassList("section-title");
            section.Add(heading);
            var body = new VisualElement();
            body.AddToClassList("section-body");
            section.Add(body);
            parent.Add(section);
            return body;
        }
    }
}
