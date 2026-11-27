using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mote.EditorTools
{
    // Each feature owns a page. Implement this interface in an Editor assembly to add it to the sidebar.
    internal interface IMoteToolPage
    {
        string Title { get; }
        string IconName { get; }
        VisualElement CreateView();
    }

    internal sealed class MoteToolsWindow : EditorWindow
    {
        internal const string Directory = "Assets/00_Mote/02_Scripts/Editor/Tools";
        [SerializeField] private string selectedPage;

        [MenuItem("Mote/에디터 도구")]
        public static void Open()
        {
            var window = GetWindow<MoteToolsWindow>();
            window.titleContent = new GUIContent("Mote Tools");
            window.minSize = new Vector2(530, 390);
            window.Show();
        }

        internal static void OpenPage(Type pageType)
        {
            Open();
            var window = GetWindow<MoteToolsWindow>();
            window.selectedPage = pageType.FullName;
            var navigation = window.rootVisualElement.Q<ListView>();
            if (navigation == null) return;
            for (int i = 0; i < navigation.itemsSource.Count; i++)
                if (navigation.itemsSource[i].GetType() == pageType)
                {
                    navigation.selectedIndex = i;
                    break;
                }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("workspace");
            root.EnableInClassList("pro-skin", EditorGUIUtility.isProSkin);
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Directory + "/MoteTools.uss"));
            var split = new TwoPaneSplitView(0, 150, TwoPaneSplitViewOrientation.Horizontal)
            {
                viewDataKey = "mote-tools-split"
            };
            root.Add(split);
            var pages = TypeCache.GetTypesDerivedFrom<IMoteToolPage>()
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Select(type => (IMoteToolPage)Activator.CreateInstance(type, true))
                .OrderBy(page => page.Title).ToArray();
            var sidebar = new VisualElement();
            sidebar.AddToClassList("sidebar");
            var project = new VisualElement();
            project.AddToClassList("project");
            var projectIcon = new Image { image = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconGenerator.Source) };
            project.Add(projectIcon);
            project.Add(new Label(PlayerSettings.productName));
            sidebar.Add(project);
            split.Add(sidebar);
            var navigation = new ListView(pages, 32, () =>
            {
                var item = new VisualElement();
                item.AddToClassList("nav-item");
                item.Add(new Image());
                item.Add(new Label());
                return item;
            }, (row, index) =>
            {
                row.Q<Label>().text = pages[index].Title;
                row.Q<Image>().image = EditorGUIUtility.IconContent(pages[index].IconName).image;
            })
            {
                selectionType = SelectionType.Single,
                showBorder = false
            };
            navigation.style.flexGrow = 1;
            sidebar.Add(navigation);
            var content = new ScrollView();
            content.AddToClassList("content");
            split.Add(content);
            navigation.selectionChanged += selection =>
            {
                var page = selection.OfType<IMoteToolPage>().FirstOrDefault();
                if (page == null) return;
                selectedPage = page.GetType().FullName;
                content.Clear();
                var header = new VisualElement();
                header.AddToClassList("page-header");
                var icon = new Image { image = EditorGUIUtility.IconContent(page.IconName).image };
                header.Add(icon);
                header.Add(new Label(page.Title));
                content.Add(header);
                var body = page.CreateView();
                body.AddToClassList("page-body");
                content.Add(body);
            };
            if (pages.Length > 0)
                navigation.selectedIndex = Math.Max(0, Array.FindIndex(pages, page => page.GetType().FullName == selectedPage));
        }
    }
}
