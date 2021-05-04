using Pepperoni;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Pepperoni.Logger;

namespace DebugMod
{
    internal class DebugHUD : MonoBehaviour
    {
        private struct NPCCache
        {
            public string NpcName { get; private set; }
            public TextAsset TextAsset { get; private set; }

            public NPCCache(string npc, TextAsset asset)
            {
                NpcName = npc;
                TextAsset = asset;
            }
        }

        static GameObject OverlayCanvas = null;
        static GameObject _textPanel;
        static TextMeshProUGUI textComp;
        static CanvasUtil.RectData topRight = new CanvasUtil.RectData(new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(0.89f, 0.80f), new Vector2(0.99f, .96f), new Vector2(0, 0));

        PlayerMachine PlayerMachine = null;
        BossController bossController = null;


        // Cached objects
        List<NPCCache> dialogueCache = new List<NPCCache>(10);
        GameObjectCache<VoidOut> collPlaneCache = new GameObjectCache<VoidOut>();
        GameObjectCache<TalkVolume> tvRadiusCache = new GameObjectCache<TalkVolume>();
        Dictionary<string, Texture2D> modTextures = new Dictionary<string, Texture2D>(2);
        StringBuilder textBuilder = new StringBuilder(500);

        // Warp cam related
        Camera warpCam = null;
        Vector3 warpSimPos = Vector3.zero;
        float yaw = 0f;
        float pitch = 0f;

        // Noclip mode
        LayerMask origMask;
        float origGrav;
        public static bool NoClipActive { get; private set; } = false;

        // UI constants
        static readonly string[] costumeNames = { "Noid", "Green", "Sanic", "Cappy" };
        static readonly string[] levelNames = { "LeviLevle", "dungeon", "PZNTv5" };
        static readonly string[] bossStates = { "Idle", "Damaged", "Movement", "WaitForIdle",
            "Dead", "WaitForIntro", "Intro", "Outro" };

        int currentCostIdx = 0;
        int currentNpcIdx = 0;
        int currentLvlIdx = 0;
        float deltaTime = 0f;

        bool _enabled = false;
        bool _visible = false;

        bool deathPlaneStatus = true;
        bool collisionRenderFlag = false;
        bool talkVolumeRenderFlag = false;

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
                _textPanel = CanvasUtil.CreateTMProPanel(_background, string.Empty, 20,
                    TextAnchor.UpperLeft,
                    new CanvasUtil.RectData(new Vector2(-5, -5), new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 1)));
                textComp = _textPanel.GetComponent<TextMeshProUGUI>();
            }

            foreach (string fn in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (fn.Contains("glass") || fn.Contains("talkVol"))
                {
                    using (Stream imageStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fn))
                    {
                        byte[] imageBuffer = new byte[imageStream.Length];
                        imageStream.Read(imageBuffer, 0, imageBuffer.Length);
                        imageStream.Flush();
                        var assetName = fn.Contains("glass") ? "glass" : "talkVol";
                        modTextures[assetName] = new Texture2D(1, 1);
                        modTextures[assetName].LoadImage(imageBuffer);
                        Logger.Log($"Loaded Texture: {assetName}");
                    }
                }
            }
            warpCam = gameObject.AddComponent<Camera>();
            warpCam.rect = new Rect(0.75f, 0f, .25f, .25f);
            warpCam.transform.position = Vector3.zero;
            warpCam.enabled = false;
        }

        private void BuildDialogueCache()
        {
            dialogueCache.Clear();
            currentNpcIdx = 0;
            var res = Resources.FindObjectsOfTypeAll<TextAsset>();
            foreach (var textAsset in res)
            {
                if (textAsset.name.StartsWith("Dia", System.StringComparison.InvariantCultureIgnoreCase) &&
                    textAsset.text.StartsWith("%n"))
                {
                    string npcName = Pepperoni.DialogueUtils.GetNPCName(textAsset.text);
                    dialogueCache.Add(new NPCCache(npcName, textAsset));
                }
            }
            dialogueCache.Sort((dia1, dia2) =>
            dia1.TextAsset.text.Length.CompareTo(dia2.TextAsset.text.Length));
        }

        private GameObject VoidOutCreator(VoidOut voidOut)
        {
            var collObj = voidOut.GetComponent<Collider>();
            Logger.LogDebug("Found Collider of type " + collObj.GetType().Name);

            if (!(collObj is BoxCollider) && !(collObj is MeshCollider))
                return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);

            if (collObj is BoxCollider)
            {
                var boxCol = collObj as BoxCollider;
                go.transform.position = boxCol.transform.TransformPoint(boxCol.center);
                go.transform.localScale = boxCol.bounds.size;
                go.GetComponent<MeshRenderer>().material.color = Color.white;
            }
            else
            {
                var meshCol = collObj as MeshCollider;
                go.transform.position = voidOut.transform.position;
                go.transform.localScale = meshCol.transform.localScale;
                go.GetComponent<MeshFilter>().mesh = meshCol.sharedMesh;
                go.GetComponent<MeshRenderer>().material.color = Color.grey;
            }

            go.GetComponent<Collider>().enabled = false;
            return go;
        }

        private GameObject TalkVolumeCreator(TalkVolume tv)
        {
            if (!modTextures.ContainsKey("talkVol"))
            {
                Logger.LogError("Missing Talk Volume Texture!");
                return null;
            }

            var collider = tv.gameObject.GetComponent<Collider>();
            if (collider is SphereCollider)
            {
                var talkSphere = collider as SphereCollider;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = tv.transform.position;
                go.transform.localScale = talkSphere.bounds.size;
                var renderer = go.GetComponent<Renderer>();
                var shader = Shader.Find("psx/trasparent/vertexlit");
                if (renderer && shader)
                {
                    for (int i = 0; i < renderer.materials.Length; ++i)
                    {
                        var matl = renderer.materials[i];
                        matl.shader = shader;
                        matl.SetTexture("_MainTex", modTextures["talkVol"]);
                        renderer.materials[i] = matl;
                    }
                }
                return go;
            }
            return null;
        }

        private void MoveWarpCam()
        {
            var SCC = PlayerMachine.controller;
            var transform = SCC.currentGround.transform;
            warpSimPos = SCC.transform.position;
            Quaternion quaternion = transform.rotation * Quaternion.Inverse(SCC.LastGroundRot);
            Vector3 PlatOffset = transform.position +
                quaternion * SCC.LastGroundOffset - (SCC.LastGroundPos + SCC.LastGroundOffset);
            warpSimPos = SCC.transform.position + PlatOffset;

            // Set up camera
            warpCam.transform.position = warpSimPos;
            warpCam.transform.rotation = Quaternion.LookRotation(PlayerMachine.lookDirection, Vector3.up);
            warpCam.farClipPlane = PlayerMachine.Camera.GetComponent<Camera>().farClipPlane;
        }

        public void ToggleState(bool enabled, PlayerMachine playerMachine)
        {
            _enabled = enabled;
            PlayerMachine = null;
            bossController = null;

            // Search for Boss
            var go = GameObject.Find("Boss Room");

            if (_enabled)
            {
                PlayerMachine = playerMachine;
                BuildDialogueCache();

                tvRadiusCache.ClearCache();
                talkVolumeRenderFlag = false;
                tvRadiusCache.UpdateCache(TalkVolumeCreator, talkVolumeRenderFlag);

                collPlaneCache.ClearCache();
                collisionRenderFlag = false;
                collPlaneCache.UpdateCache(VoidOutCreator, collisionRenderFlag);

                if (go)
                {
                    bossController = go.GetComponent<BossRoomController>().Boss;
                }
            }
        }

        private void SetupNoClip(bool toggle)
        {
            if (toggle)
            {
                origMask = PlayerMachine.controller.Walkable;
                origGrav = PlayerMachine.Gravity;
                PlayerMachine.controller.currentGround.walkable = 0;
                PlayerMachine.controller.Walkable = 0;
            }
            else
            {
                PlayerMachine.controller.currentGround.walkable = origMask;
                PlayerMachine.controller.Walkable = origMask;
                PlayerMachine.Gravity = origGrav;
            }

            NoClipActive = toggle;

        }

        string OnOffStr(bool val) => val ? "ON" : "OFF";

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                _visible = !_visible;
                var cg = OverlayCanvas.GetComponent<CanvasGroup>();
                StartCoroutine(_visible
                    ? CanvasUtil.FadeInCanvasGroup(cg)
                    : CanvasUtil.FadeOutCanvasGroup(cg));
            }

            textComp.text = "";

            if (!_enabled || !_visible || PlayerMachine == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                bool? a = PlayerMachine.CoyoteFrameEnabled;
                PlayerMachine.CoyoteFrameEnabled =
                    a.HasValue ? (a.Value ? false : new bool?()) : true;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (--currentCostIdx < 0)
                    currentCostIdx = costumeNames.Length - 1;
            }
            else if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                currentCostIdx = (currentCostIdx + 1) % (costumeNames.Length);
            }
            else if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                PlayerMachine.SetCostume((Costumes)currentCostIdx);
                GameObject.FindGameObjectWithTag("Manager").GetComponent<DialogueSystem>().UpdateCostumePortrait();
            }

            if (Input.GetKeyDown(KeyCode.Comma))
            {
                if (--currentNpcIdx < 0)
                    currentNpcIdx = dialogueCache.Count - 1;
            }
            else if (Input.GetKeyDown(KeyCode.Period))
            {
                currentNpcIdx = (currentNpcIdx + 1) % dialogueCache.Count;
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                GameObject.FindGameObjectWithTag("Manager").GetComponent<DialogueSystem>().Begin(dialogueCache[currentNpcIdx].TextAsset, null);
            }

            if (Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                if (Time.timeScale > 0)
                {
                    float timeScale = Time.timeScale / 2;
                    if (timeScale < 0.25f) timeScale = 2.0f;
                    Time.timeScale = timeScale;
                }
            }

            if ((Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetMouseButtonDown(4))
                && !PlayerMachine.currentState.Equals(PlayerStates.Loading))
            {
                PlayerMachine.EndScene();
            }

            if (Input.GetKeyDown(KeyCode.F6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                ++QualitySettings.vSyncCount;
                QualitySettings.vSyncCount %= 3;
            }

            bool inVoid = SceneManager.GetActiveScene().name == "void";
            if ((Input.GetKeyDown(KeyCode.F7) || Input.GetKeyDown(KeyCode.Keypad7)) && !PlayerMachine.currentState.Equals(PlayerStates.Loading))
            {
                var pizza = FindObjectOfType<PizzaBox>();
                if (pizza == null)
                {
                    Logger.LogError("No PizzaBox!");
                }
                else
                {
                    string level = (!inVoid) ? "void" : levelNames[currentLvlIdx];
                    deathPlaneStatus = true;
                    GameObject.Find("Global Manager").GetComponent<Manager>().LoadScene(level, pizza.ExitId, pizza.gameObject);
                }
            }
            else if (inVoid && Input.GetKeyDown(KeyCode.L))
            {
                do
                {
                    currentLvlIdx = (currentLvlIdx + 1) % levelNames.Length;
                } while (levelNames[currentLvlIdx] == SceneManager.GetActiveScene().name);
            }

            if (!inVoid && Input.GetKeyDown(KeyCode.K))
            {
                var key = FindObjectsOfType<Key>();
                foreach (var k in key)
                {
                    if (!k.pickedUp)
                    {
                        k.transform.position = PlayerMachine.gameObject.transform.position;
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                talkVolumeRenderFlag = !talkVolumeRenderFlag;
                if (talkVolumeRenderFlag)
                    tvRadiusCache.UpdateCache(TalkVolumeCreator, talkVolumeRenderFlag);
                else
                    tvRadiusCache.ToggleAllObjects(talkVolumeRenderFlag);
            }

            if (Input.GetKeyDown(KeyCode.T) && !PlayerMachine.currentState.Equals(PlayerStates.Loading))
                SetupNoClip(!NoClipActive);

            if (Input.GetKeyDown(KeyCode.M))
            {
                var deathPlanes = FindObjectsOfType<VoidOut>();
                deathPlaneStatus = !deathPlaneStatus;
                foreach (var d in deathPlanes)
                {
                    d.setActive(deathPlaneStatus);
                }
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                collisionRenderFlag = !collisionRenderFlag;
                if (collisionRenderFlag)
                    collPlaneCache.UpdateCache(VoidOutCreator, collisionRenderFlag);
                else
                    collPlaneCache.ToggleAllObjects(collisionRenderFlag);
            }

            if ((Input.GetMouseButtonDown(3) || Input.GetKeyDown(KeyCode.R))
                && (PlayerMachine.currentState.Equals(PlayerStates.Jump) ||
                PlayerMachine.currentState.Equals(PlayerStates.Loading))
                && warpCam.enabled)
            {
                MoveWarpCam();

                yaw = warpCam.transform.eulerAngles.y;
                pitch = warpCam.transform.eulerAngles.x;
            }
            else if (Input.GetMouseButton(2))
            {
                yaw += Input.GetAxis("Mouse X") * 2f;
                pitch -= Input.GetAxis("Mouse Y") * 2f;
                warpCam.transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                warpCam.enabled = !warpCam.enabled;
            }

            if (Input.GetKeyDown(KeyCode.N) && modTextures.ContainsKey("glass"))
            {
                if (Physics.Raycast(
                    PlayerMachine.transform.position + PlayerMachine.controller.up * PlayerMachine.controller.height * 0.85f,
                    PlayerMachine.lookDirection,
                    out RaycastHit objHit))
                {
                    Logger.LogFine($"Hit an object: {objHit.collider.gameObject.name}");
                    var renderer = objHit.collider.gameObject.GetComponent<Renderer>();
                    var shader = Shader.Find("psx/trasparent/vertexlit");
                    if (renderer && shader)
                    {
                        for (int i = 0; i < renderer.materials.Length; ++i)
                        {
                            var matl = renderer.materials[i];
                            matl.shader = shader;
                            matl.SetTexture("_MainTex", modTextures["glass"]);
                            matl.SetColor("_Color", Color.blue);
                            renderer.materials[i] = matl;
                        }
                        GameObject go = new GameObject();
                        Vector3 start = PlayerMachine.transform.position + PlayerMachine.controller.up * PlayerMachine.controller.height * 0.85f;
                        go.transform.position = start;
                        var lineRenderer = go.AddComponent<LineRenderer>();
                        lineRenderer.material = new Material(Shader.Find("Standard"));
                        lineRenderer.material.SetColor("_Color", Color.red);
                        lineRenderer.startColor = Color.red;
                        lineRenderer.endColor = Color.red;
                        lineRenderer.startWidth = 0.1f;
                        lineRenderer.SetPosition(0, start);
                        lineRenderer.SetPosition(1, objHit.point);
                        Destroy(go, 0.3f);
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.Keypad8))
            {
                foreach (var e in FindObjectsOfType<HiddenPlatform>())
                {
                    if (!e.cameraActive) e.flag = false;
                }
            }

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            bool? b = PlayerMachine.CoyoteFrameEnabled;

            textBuilder.Length = 0;
            textBuilder.AppendFormat("FPS: {0:F3}\n", 1f / deltaTime);
            textBuilder.AppendFormat("<F1>: Coyote Frames: {0}\n", b.HasValue ? OnOffStr(b.Value) : "Default");
            textBuilder.Append("<F2><[]>: Set Costume: ").AppendLine(costumeNames[currentCostIdx]);
            textBuilder.AppendFormat("<F3><,.>: Dialogue: ").AppendLine(dialogueCache[currentNpcIdx].NpcName);
            textBuilder.Append("<F4>: Time Scale: x").AppendLine(Time.timeScale.ToString("F2"));
            textBuilder.Append("<F5>: Text Storage/Warp\n");
            textBuilder.AppendFormat("<F6>: VSync Count :{0}\n", QualitySettings.vSyncCount);
            textBuilder.AppendFormat("<F7><L>: Level Load: {0}\n", !inVoid ? "void" : levelNames[currentLvlIdx]);
            textBuilder.Append("<F8>: Reset Camera Events\n");
            textBuilder.Append("<K>: Get All Keys\n");
            textBuilder.Append("<V>: Render Death Planes: ").AppendLine(OnOffStr(collisionRenderFlag));
            textBuilder.Append("<M>: Active Death Planes: ").AppendLine(OnOffStr(deathPlaneStatus));
            textBuilder.Append("<G>: Show NPC Talk Zone: ").AppendLine(OnOffStr(talkVolumeRenderFlag));
            textBuilder.Append("<R><noparse><B></noparse>: Warp Cam Move/Toggle\n");
            textBuilder.Append("<N>: Set Obj Transparent\n");
            textBuilder.Append("<T>: NoClip: ").AppendLine(OnOffStr(NoClipActive));
            textBuilder.Append("<F11> Toggle UI\n").AppendLine();

            textBuilder.Append("Move Dir:").AppendLine(PlayerMachine.moveDirection.ToString());
            textBuilder.Append("Pos:").AppendLine(PlayerMachine.transform.position.ToString());
            textBuilder.Append("SpawnPos:").AppendLine(PlayerMachine.LastGroundLoc.ToString());
            textBuilder.Append("Look Dir:").AppendLine(PlayerMachine.lookDirection.ToString());
            textBuilder.Append("Player State:").AppendLine(PlayerMachine.currentState.ToString());

            if (bossController)
            {
                textBuilder.AppendLine();
                textBuilder.Append("Boss Health: ").AppendLine(bossController.Health.ToString());
                textBuilder.Append("Boss State: ").AppendLine(bossStates[(int)BossController.State]);
            }
            if (warpCam.enabled)
            {
                textBuilder.AppendLine();
                textBuilder.Append("LGP:").AppendLine(PlayerMachine.controller.LastGroundPos.ToString());
                textBuilder.Append("LGO:").AppendLine(PlayerMachine.controller.LastGroundOffset.ToString());
                textBuilder.Append("LGR:").AppendLine(PlayerMachine.controller.LastGroundRot.ToString());
                textBuilder.Append("CG:").AppendLine(PlayerMachine.controller.currentGround.transform.position.ToString());
                textBuilder.Append("CR:").AppendLine(PlayerMachine.controller.currentGround.transform.rotation.ToString());
                textBuilder.Append("Warp Pos:").AppendLine(warpSimPos.ToString());

            }

            textComp.SetText(textBuilder);
        }
    }
}
