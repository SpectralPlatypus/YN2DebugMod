using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Pepperoni;
using Logger = Pepperoni.Logger;
using System.Reflection;

namespace DebugMod
{
    internal class DebugHUD : MonoBehaviour
    {
        private struct NPCCache
        {
            public string npcName;
            public TextAsset textAsset;

            public NPCCache(string npc, TextAsset asset)
            {
                npcName = npc;
                textAsset = asset;
            }
        }

        public static GameObject OverlayCanvas = null;
        private static GameObject _textPanel;
        private static CanvasUtil.RectData topRight = new CanvasUtil.RectData(new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(0.85f, 0.80f), new Vector2(0.95f, .96f), new Vector2(0, 0));
        private static PlayerMachine PlayerMachine = null;
        private bool _enabled = false;

        private List<NPCCache> dialogues = new List<NPCCache>(10);

        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (OverlayCanvas == null)
            {
                CanvasUtil.CreateFonts();
                OverlayCanvas = CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));
                OverlayCanvas.name = "DebugMenu";
                DontDestroyOnLoad(OverlayCanvas);

                GameObject _background = CanvasUtil.CreateImagePanel(OverlayCanvas, new Color32(0x28, 0x28, 0x28, 0x00), topRight);
                _textPanel = CanvasUtil.CreateTMProPanel(_background, string.Empty, 24,
                    TextAnchor.UpperLeft,
                    new CanvasUtil.RectData(new Vector2(-5, -5), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 1)));
            }
        }

        private void BuildDialogueCache()
        {
            dialogues.Clear();
            currentNpcIdx = 0;
            var res = Resources.FindObjectsOfTypeAll<TextAsset>();
            foreach(var textAsset in res)
            {
                if (textAsset.name.StartsWith("Dia", System.StringComparison.InvariantCultureIgnoreCase) &&
                    textAsset.text.StartsWith("%n"))
                {
                    string npcName = Pepperoni.DialogueUtils.GetNPCName(textAsset.text);
                    if (npcName == string.Empty || npcName == "Denise") continue;
                    dialogues.Add(new NPCCache(npcName, textAsset));
                }
            }
        }

        public void ToggleState(bool enabled, PlayerMachine playerMachine)
        {
            _enabled = enabled;
            PlayerMachine = (_enabled) ? playerMachine : null;

            if (_enabled) BuildDialogueCache();
        }

        private static readonly int MAX_COSTUME_INDEX = 3;
        private static string[] costumeNames = { "Noid", "Green", "Sanic", "Cappy" };
        private static string[] levelNames = { "LeviLevle", "dungeon", "PZNTv5" };
        private int currentCostIdx = 0;
        private int currentNpcIdx = 0;
        private int currentLvlIdx = 0;
        private float deltaTime = 0f;
        private bool _visible = false;

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                _visible = !_visible;
                StartCoroutine(_visible
                    ? CanvasUtil.FadeInCanvasGroup(OverlayCanvas.GetComponent<CanvasGroup>())
                    : CanvasUtil.FadeOutCanvasGroup(OverlayCanvas.GetComponent<CanvasGroup>()));
            }

            var t = _textPanel.GetComponent<TextMeshProUGUI>();
            t.text = "";

            if (!_enabled || !_visible || PlayerMachine == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                bool? a = PlayerMachine.CoyoteFrameEnabled;
                if (a.HasValue)
                {
                    if (a.Value) a = false;
                    else a = null;
                }
                else
                    a = true;
                Manager.Player.GetComponent<PlayerMachine>().CoyoteFrameEnabled = a;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (--currentCostIdx < 0)
                    currentCostIdx = MAX_COSTUME_INDEX;
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                currentCostIdx = (currentCostIdx + 1) % (MAX_COSTUME_INDEX + 1);
            }
            else if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                PlayerMachine.SetCostume((Costumes)currentCostIdx);
                GameObject.FindGameObjectWithTag("Manager").GetComponent<DialogueSystem>().UpdateCostumePortrait();
            }

            if (Input.GetKeyDown(KeyCode.Comma))
            {
                if (--currentNpcIdx < 0)
                    currentNpcIdx = dialogues.Count - 1;
            }
            else if (Input.GetKeyDown(KeyCode.Period))
            {
                currentNpcIdx = (currentNpcIdx + 1) % dialogues.Count;
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                GameObject.FindGameObjectWithTag("Manager").GetComponent<DialogueSystem>().Begin(dialogues[currentNpcIdx].textAsset, null);
            }

            if (Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                if (Time.timeScale > 0)
                {
                    float timeScale = Time.timeScale / 2;
                    if (timeScale < 0.25f) timeScale = 1.0f;
                    Time.timeScale = timeScale;
                }
            }

            if (Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                PlayerMachine.EndScene();
            }

            if (Input.GetKeyDown(KeyCode.F6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                if (QualitySettings.vSyncCount == 2)
                {
                    QualitySettings.vSyncCount = 0;
                }
                else
                {
                    QualitySettings.vSyncCount++;
                }
            }

            bool inVoid = SceneManager.GetActiveScene().name == "void";
            if ((Input.GetKeyDown(KeyCode.F7) || Input.GetKeyDown(KeyCode.Keypad7)) && !PlayerMachine.currentState.Equals(PlayerStates.Loading))
            {
                var pizza = FindObjectOfType<PizzaBox>();
                if (pizza == null)
                {
                    Pepperoni.Logger.LogDebug("No PizzaBox!");
                }
                else
                {
                    string level = (!inVoid) ? "void" : levelNames[currentLvlIdx];
                    GameObject.Find("Global Manager").GetComponent<Manager>().LoadScene(level, pizza.ExitId, pizza.gameObject);
                }
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                do
                {
                    currentLvlIdx = (currentLvlIdx + 1) % levelNames.Length;
                } while (levelNames[currentLvlIdx] == SceneManager.GetActiveScene().name);
            }

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            bool? b = PlayerMachine.CoyoteFrameEnabled;

            t.text += "FPS: " + 1f / deltaTime + "\n";
            t.text += "<F1>: Coyote Frames: " + (b.HasValue ? b.Value.ToString() : "Default") + "\n";
            t.text += "<F2><[]>: Set Costume: " + costumeNames[currentCostIdx] + "\n";
            if(dialogues.Count > 0) t.text += "<F3><,.>: Dialogue: " + dialogues[currentNpcIdx].npcName + "\n";
            t.text += "<F4>: Time Scale: x" + Time.timeScale.ToString("F2") + "\n";
            t.text += "<F5>: Text Storage/Warp?\n";
            t.text += "<F6>: VSync Count :" + QualitySettings.vSyncCount + "\n";
            t.text += "<F7><L> Level Load: " + (!inVoid ? "void" : levelNames[currentLvlIdx]) + "\n";
            t.text += "<F11> Toggle UI\n\n";
            t.text += "Move Dir:" + PlayerMachine.moveDirection.ToString() + "\n";
            t.text += "Pos:" + PlayerMachine.transform.position + "\n";
            t.text += "SpawnPos:" + PlayerMachine.LastGroundLoc.ToString() + "\n";
            t.text += "Player State:" + PlayerMachine.currentState.ToString() + "\n";
        }
    }
}
