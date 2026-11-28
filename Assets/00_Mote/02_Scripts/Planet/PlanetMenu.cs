using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using System.Threading.Tasks;
using DoHwa.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoHwa.Planet
{
    public sealed class PlanetMenu : MonoBehaviour
    {
        [SerializeField] private PlanetView globe;
        [SerializeField] private PlanetDecorationCatalog catalog;
        [SerializeField] private Transform placed;
        [SerializeField] private GameObject dock, utilities, shade, decorate, visit, journal, placement;
        [SerializeField] private TMP_Text header, placementHint, noteList, pageLabel, address;
        [SerializeField] private TMP_InputField destination, message;
        [SerializeField] private Button save, remove, send, travel, prev, next, copy, home, editButton;
        [SerializeField] private RectTransform bottomArea;
        [SerializeField] private GameObject[] entryRows;
        [SerializeField] private TMP_Text[] entryBodies, entryAuthors;
        [SerializeField] private Button[] revisit;
        [SerializeField] private GameObject[] shelves;
        [SerializeField] private Button[] shelfTabs, giftChoices;
        [SerializeField] private TMP_Text capacity, shelfHint, dockAction, giftHint;
        [SerializeField] private GameObject giftSheet, utilityToggle;
        private static readonly string[] ShelfHints = { "초지에 심을 장식", "함께 머물 자리를 꾸며요", "친구가 열매를 놓을 자리를 열어요" };
        private readonly List<PlanetItem> draft = new();
        private PlanetData data = new();
        private bool busy, editing;
        [SerializeField] private Button rotate, undo;
        [SerializeField] private Image actionIcon;
        [SerializeField] private Sprite craftIcon, fruitIcon;
        private PlanetItem[] previousEdit;
        private int lastPlaced = -1;
        [SerializeField] private Image[] dockSelections, dockGlyphs, shelfSelections, shelfGlyphs;
        [SerializeField] private Color selectedInk, idleInk;
        private int tool, page, selectedShelf;
        private GameObject currentPanel;
        private bool utilitiesOpen;
        private string pendingId, pendingText;

        private bool Visiting => !string.IsNullOrEmpty(data.owner) && data.owner != PlanetApi.Player;
        private bool Changed => !draft.SequenceEqual(data.items);
        private int grabbed = -1;
        private void OnEnable()
        {
            globe.SurfaceTapped += Tap;
            globe.TryGrab += Grab;
            globe.MoveGrab += MoveGrab;
            globe.EndGrab += EndGrab;
        }
        private void OnDisable()
        {
            EndGrab(false);
            globe.SurfaceTapped -= Tap;
            globe.TryGrab -= Grab;
            globe.MoveGrab -= MoveGrab;
            globe.EndGrab -= EndGrab;
        }
        private bool Grab(Vector3 direction)
        {
            if (busy || !editing || currentPanel != placement || tool < 0) return false;
            grabbed = draft.FindIndex(item => Vector3.Angle(item.Direction, direction) < 9f);
            return grabbed >= 0;
        }
        private void MoveGrab(Vector3 n)
        {
            if (grabbed < 0) return;
            var model = placed.GetChild(grabbed);
            model.localPosition = globe.SurfacePosition(n);
            model.localRotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.AngleAxis(draft[grabbed].yaw, Vector3.up);
        }
        private void EndGrab(bool commit)
        {
            if (grabbed < 0) return;
            int index = grabbed;
            var n = placed.GetChild(index).localPosition.normalized;
            if (commit)
            {
                for (int i = 0; i < draft.Count; i++)
                    if (i != index && Vector3.Angle(draft[i].Direction, n) < 9f) commit = false;
            }
            if (commit)
            {
                previousEdit = draft.ToArray();
                var item = draft[index];
                item.x = n.x;
                item.y = n.y;
                item.z = n.z;
                draft[index] = item;
                lastPlaced = index;
                Buttons();
            }
            else MoveGrab(draft[index].Direction.normalized);
            grabbed = -1;
        }
        private void Start()
        {
            destination.onValueChanged.AddListener(_ => Buttons());
            message.onValueChanged.AddListener(_ => Buttons());
            utilities.SetActive(false);
            SetTab(dockSelections, dockGlyphs, 0);
            Shelf(0);
            Buttons();
            if (PlanetApi.Player != null) Run(async () => Apply(await PlanetApi.Read(PlanetApi.Player)));
        }
        private void Update()
        {
            if (SystemUI.PopupCapturesInput || busy || currentPanel == null || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
            if (currentPanel == placement) Palette(); else Close();
        }
        private void LateUpdate()
        {
            var min = bottomArea.anchorMin;
            min.y = Screen.safeArea.yMin / Mathf.Max(1, Screen.height);
            if (bottomArea.anchorMin != min) bottomArea.anchorMin = min;
        }
        private void Run(Func<Task> work)
        {
            if (!busy) Execute(work);
        }
        private async void Execute(Func<Task> work)
        {
            busy = true;
            Buttons();
            try { await work(); }
            catch (Exception e) { if (this) SystemUI.ShowNotice("내 행성", e is InvalidOperationException ? e.Message : "다시 시도해 주세요."); }
            finally { if (this) { busy = false; Buttons(); } }
        }
        private void Apply(PlanetData value)
        {
            if (!this) return;
            data = value;
            Restore();
            header.text = Visiting ? "친구의 행성" : "내 행성";
            Buttons();
        }
        private void Restore()
        {
            draft.Clear();
            draft.AddRange(data.items);
            previousEdit = null;
            lastPlaced = -1;
            RebuildPlaced();
        }
        private void RebuildPlaced()
        {
            while (placed.childCount > 0)
            {
                var child = placed.GetChild(placed.childCount-1);
                child.gameObject.SetActive(false);
                child.SetParent(null);
                Destroy(child.gameObject);
            }
            foreach (var item in draft) Draw(item);
        }
        private void Draw(PlanetItem item) => catalog.Draw(item, globe, placed);

        private static void Availability(Button button, bool available)
        {
            button.interactable = available;
            button.GetComponent<CanvasGroup>().alpha = available ? 1f : .54f;
        }
        private void Buttons()
        {
            Availability(editButton, !busy);
            dockAction.text = Visiting ? "열매 선물" : "꾸미기";
            actionIcon.sprite = Visiting ? fruitIcon : craftIcon;
            Availability(undo, !busy && previousEdit != null);
            rotate.gameObject.SetActive(tool >= 0);
            Availability(rotate, lastPlaced >= 0);
            capacity.text = draft.Count+" / 48";
            for (int i=0; i<giftChoices.Length; i++)
                Availability(giftChoices[i], !busy && OpenSlot(i) >= 0);
            Availability(save, !busy && Changed);
            Availability(remove, draft.Count > 0);
            Availability(travel, !busy && !string.IsNullOrWhiteSpace(destination.text));
            Availability(copy, PlanetApi.Player != null);
            home.gameObject.SetActive(Visiting);
            Availability(send, !busy && !string.IsNullOrWhiteSpace(message.text));
            Availability(prev, page > 0);
            Availability(next, (page+1)*3 < data.notes.Length);
        }
        private void Show(GameObject panel)
        {
            EndGrab(false);
            currentPanel = panel;
            if (panel == decorate || panel == giftSheet || panel == visit || panel == journal)
                SetTab(dockSelections, dockGlyphs, panel == visit ? 1 : panel == journal ? 2 : 0);
            utilitiesOpen = false;
            SetShown(dock, panel == null);
            SetShown(utilities, false);
            SetShown(utilityToggle, panel == null);
            SetShown(giftSheet, panel == giftSheet);
            SetShown(shade, panel != null && panel != placement);
            if (panel == decorate && !decorate.activeSelf)
                for (int i = 0; i < shelves.Length; i++)
                    shelves[i].SetActive(i == selectedShelf);
            SetShown(decorate, panel == decorate);
            SetShown(journal, panel == journal);
            SetShown(visit, panel == visit);
            SetShown(placement, panel == placement);
            Buttons();
        }

        private static async void SetShown(GameObject target, bool shown)
        {
            if (!target.activeInHierarchy)
            {
                target.SetActive(shown);
                return;
            }
            var reveal = target.GetComponent<UiPanelReveal>();
            if (shown)
            {
                if (reveal.IsLeaving) reveal.Replay();
                return;
            }
            if (reveal.IsLeaving) return;
            var group = target.GetComponent<CanvasGroup>();
            group.interactable = false;
            await reveal.HideAsync();
            if (target && reveal.IsClosed) target.SetActive(false);
        }

        public void ToggleUtilities()
        {
            utilitiesOpen = !utilitiesOpen;
            SetShown(utilities, utilitiesOpen);
        }
        public void Shelf(int index)
        {
            for (int i=0; i<shelves.Length; i++)
            {
                if (i == index || selectedShelf == i) SetShown(shelves[i], i == index);
                else shelves[i].SetActive(false);

            }
            SetTab(shelfSelections, shelfGlyphs, index);
            selectedShelf = index;
            shelfHint.text = ShelfHints[index];
        }
        private void SetTab(Image[] selections, Image[] glyphs, int selected)
        {
            for (int i = 0; i < selections.Length; i++)
            {
                selections[i].enabled = i == selected;
                glyphs[i].color = i == selected ? selectedInk : idleInk;
            }
        }
        private int OpenSlot(int site) => Array.FindIndex(data.items, i=>i.kind==catalog[6+site].kind && string.IsNullOrEmpty(i.giftFrom));
        private void GiftSheet()
        {
            giftHint.text = OpenSlot(0)>=0 || OpenSlot(1)>=0 ? "열매를 놓을 자리를 골라 주세요." : "지금은 비어 있는 선물 자리가 없어요.";
            Show(giftSheet);
        }
        public void Give(int site) => Run(async () =>
        {
            int slot = OpenSlot(site);
            if (!Visiting || slot < 0) return;
            var targetDirection = data.items[slot].Direction;
            try { await PlanetApi.LeaveGift(data.owner, data.revision, slot); }
            finally
            {
                if (this) { var updated = await PlanetApi.Read(data.owner); if (this) { Apply(updated); GiftSheet(); } }
            }
            if (!this) return;
            globe.Face(targetDirection);
            Show(null);
            SystemUI.ShowNotice("열매 선물", site==0 ? "연못에 사과등이 떠올랐어요." : "언덕에 사과 구름이 피어났어요.");
        });
        public void Close()
        {
            if (busy) return;
            if (editing && Changed)
            {
                SystemUI.Confirm("행성 꾸미기", "저장하지 않은 배치를 되돌릴까요?", "되돌리기", Discard);
                return;
            }
            Discard();
        }
        private void Discard()
        {
            if (editing) Restore();
            editing = false;
            Show(null);
        }
        public void Decorate()
        {
            if (busy) return;
            if (Visiting) { GiftSheet(); return; }
            editing = true;
            Show(decorate);
            if (PlanetApi.Player != null) Run(async () => Apply(await PlanetApi.Read(PlanetApi.Player)));
        }
        public void SelectTool(int index)
        {
            if (!editing || busy) return;
            tool = index;
            lastPlaced = -1;
            placementHint.text = index < 0 ? "거둘 장식을 눌러 주세요" : catalog[index].displayName+" · 눌러 놓기 / 끌어 옮기기";
            Show(placement);
        }
        public void Palette() => Show(decorate);
        private float lastHomeTap = -1f;
        private Vector3 lastHomeDirection;
        private void Tap(Vector3 n)
        {
            if (busy || SystemUI.PopupCapturesInput) return;
            if (!editing && currentPanel == null && !utilitiesOpen)
            {
                if (Time.unscaledTime - lastHomeTap < .35f && Vector3.Angle(lastHomeDirection, n) < 7f)
                {
                    globe.ToggleDistance();
                    lastHomeTap = -1f;
                    return;
                }
                lastHomeTap = Time.unscaledTime;
                lastHomeDirection = n;
            }
            if (!editing && currentPanel == null && !Visiting && !utilitiesOpen)
            {
                var received = Array.Find(data.items,i=>!string.IsNullOrEmpty(i.giftFrom) && Vector3.Angle(i.Direction,n)<9f);
                if (!string.IsNullOrEmpty(received.giftFrom))
                    SystemUI.Confirm("도착한 열매", "선물을 남긴 친구의 행성에 가볼까요?", "답방하기", ()=>Load(received.giftFrom));
                return;
            }
            if (!editing || currentPanel != placement) return;
            int near = draft.FindIndex(i => Vector3.Angle(i.Direction,n) < 9f);
            if (tool < 0)
            {
                if (near < 0) return;
                previousEdit = draft.ToArray();
                lastPlaced = -1;
                placed.GetChild(near).gameObject.SetActive(false);
                var old = placed.GetChild(near);
                old.SetParent(null);
                Destroy(old.gameObject);
                draft.RemoveAt(near);
            }
            else
            {
                if (near >= 0) { placementHint.text = "조금 더 떨어진 곳에 놓아 주세요"; return; }
                if (draft.Count >= 48) { placementHint.text = "장식을 모두 놓았어요"; return; }
                var item = new PlanetItem { kind=catalog[tool].kind, x=n.x, y=n.y, z=n.z };
                previousEdit = draft.ToArray();
                lastPlaced = draft.Count;
                draft.Add(item);
                Draw(item);
            }
            Buttons();
        }
        public void RotatePlaced()
        {
            if (busy || !editing || lastPlaced < 0 || lastPlaced >= draft.Count) return;
            previousEdit = draft.ToArray();
            var item = draft[lastPlaced];
            item.yaw = (item.yaw+45)%360;
            draft[lastPlaced] = item;
            placed.GetChild(lastPlaced).localRotation = Quaternion.FromToRotation(Vector3.up,item.Direction.normalized)
                * Quaternion.AngleAxis(item.yaw,Vector3.up);
            Buttons();
        }
        public void UndoEdit()
        {
            if (busy || previousEdit==null) return;
            draft.Clear();
            draft.AddRange(previousEdit);
            previousEdit=null;
            lastPlaced=-1;
            RebuildPlaced();
            Buttons();
        }
        public void SaveLayout() => Run(async () =>
        {
            await PlanetApi.Save(data.revision,draft.ToArray());
            if (!this) return;
            data.items = draft.ToArray();
            data.revision++;
            editing = false;
            Show(null);
        });
        public void Visit()
        {
            address.text = PlanetApi.Player ?? "로그인 후 확인할 수 있어요";
            Show(visit);
        }
        public void Copy()
        {
            GUIUtility.systemCopyBuffer = PlanetApi.Player;
            address.text = "내 행성 주소를 복사했어요";
        }
        public void Travel() => Load(destination.text.Trim());
        public void ReturnHome() => Load(PlanetApi.Player);
        private void Load(string id) => Run(async () =>
        {
            var planet = await PlanetApi.Read(id);
            if (!this) return;
            Apply(planet);
            destination.text = message.text = "";
            pendingId = null;
            Show(null);
        });
        public void Journal()
        {
            page = 0;
            DisplayNotes();
            Show(journal);
            if (!string.IsNullOrEmpty(data.owner) || PlanetApi.Player != null) Run(async () =>
            {
                var latest = await PlanetApi.Read(string.IsNullOrEmpty(data.owner) ? PlanetApi.Player : data.owner);
                if (!this) return;
                Apply(latest);
                DisplayNotes();
            });
        }
        private void DisplayNotes()
        {
            noteList.gameObject.SetActive(data.notes.Length==0);
            noteList.text = "첫 인사를 기다리고 있어요.";
            for (int row=0; row<entryRows.Length; row++)
            {
                int index=page*3+row;
                entryRows[row].SetActive(index<data.notes.Length);
                if (index>=data.notes.Length) continue;
                var note=data.notes[index];
                entryAuthors[row].text=note.author==PlanetApi.Player ? "내가 남긴 인사" : "방문자가 남긴 인사";
                entryBodies[row].text=note.text;
                revisit[row].gameObject.SetActive(note.author!=data.owner && note.author!=PlanetApi.Player);
            }
            pageLabel.text = (page+1)+" / "+Mathf.Max(1,(data.notes.Length+2)/3);
            Buttons();
        }
        public void VisitWriter(int row)
        {
            int index=page*3+row;
            if (index>=0 && index<data.notes.Length) Load(data.notes[index].author);
        }
        public void Turn(int direction)
        {
            int nextPage = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (data.notes.Length - 1) / 3));
            if (nextPage == page) return;
            page = nextPage;
            DisplayNotes();
            journalEntries.Replay();
        }
        [SerializeField] private UiPanelReveal journalEntries;
        public void SendNote() => Run(async () =>
        {
            string body = message.text.Trim();
            if (pendingId == null || pendingText != body) { pendingText = body; pendingId = Guid.NewGuid().ToString("N"); }
            if (string.IsNullOrEmpty(data.owner)) throw new InvalidOperationException("타이틀에서 로그인한 뒤 이용할 수 있어요.");
            await PlanetApi.Write(data.owner,pendingId,body);
            var latest = await PlanetApi.Read(data.owner);
            if (!this) return;
            Apply(latest);
            pendingId = null;
            message.text = "";
            page = 0;
            DisplayNotes();
        });
    }
}
