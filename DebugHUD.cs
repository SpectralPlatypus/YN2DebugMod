using Pepperoni;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
                    new Vector2(0.89f, 0.80f), new Vector2(0.99f, .96f), new Vector2(0, 0));
        private static PlayerMachine PlayerMachine = null;
        private static BossController bossController = null;
        private bool _enabled = false;
        private float yaw = 0f;
        private float pitch = 0f;

        List<NPCCache> dialogues = new List<NPCCache>(10);
        GameObjectCache<VoidOut> collPlaneCache = new GameObjectCache<VoidOut>();
        GameObjectCache<TalkVolume> tvRadiusCache = new GameObjectCache<TalkVolume>();
        Dictionary<string, Texture2D> modTextures = new Dictionary<string, Texture2D>(2);
        Camera warpCam = null;

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
            dialogues.Clear();
            currentNpcIdx = 0;
            var res = Resources.FindObjectsOfTypeAll<TextAsset>();
            foreach (var textAsset in res)
            {
                if (textAsset.name.StartsWith("Dia", System.StringComparison.InvariantCultureIgnoreCase) &&
                    textAsset.text.StartsWith("%n"))
                {
                    string npcName = Pepperoni.DialogueUtils.GetNPCName(textAsset.text);
                    dialogues.Add(new NPCCache(npcName, textAsset));
                }
            }
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
            go.GetComponent<MeshRenderer>().receiveShadows = false;
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
                go.GetComponent<MeshRenderer>().receiveShadows = false;
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

        LayerMask origMask;
        float origGrav;
        public static bool NoClipActive = false;
        private void SetupNoClip(bool toggle)
        {
            if(toggle)
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

        private static readonly int MAX_COSTUME_INDEX = 3;
        private static string[] costumeNames = { "Noid", "Green", "Sanic", "Cappy" };
        private static string[] levelNames = { "LeviLevle", "dungeon", "PZNTv5" };
        private static string[] bossStates = { "Idle", "Damaged", "Movement", "WaitForIdle",
            "Dead", "WaitForIntro", "Intro", "Outro" };
        private int currentCostIdx = 0;
        private int currentNpcIdx = 0;
        private int currentLvlIdx = 0;
        private float deltaTime = 0f;
        private bool _visible = false;
        private bool deathPlaneStatus = true;
        private bool collisionRenderFlag = false;
        private bool talkVolumeRenderFlag = false;
        private Vector3 warpSimPos = new Vector3(0f, 0f, 0f);
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
                PlayerMachine.CoyoteFrameEnabled =
                    a.HasValue ? (a.Value ? false : new bool?()) : true;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (--currentCostIdx < 0)
                    currentCostIdx = MAX_COSTUME_INDEX;
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
                    Logger.LogDebug("No PizzaBox!");
                }
                else
                {
                    string level = (!inVoid) ? "void" : levelNames[currentLvlIdx];
                    deathPlaneStatus = true;
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

            if (Input.GetKeyDown(KeyCode.K))
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

            if(Input.GetKeyDown(KeyCode.T) && !PlayerMachine.currentState.Equals(PlayerStates.Loading))
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
                if(collisionRenderFlag)
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

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            bool? b = PlayerMachine.CoyoteFrameEnabled;

            t.text += "FPS: " + 1f / deltaTime + "\n";
            t.text += "<F1>: Coyote Frames: " + (b.HasValue ? (b.Value ? "ON" : "OFF") : "Default") + "\n";
            t.text += "<F2><[]>: Set Costume: " + costumeNames[currentCostIdx] + "\n";
            if (dialogues.Count > 0) t.text += "<F3><,.>: Dialogue: " + dialogues[currentNpcIdx].npcName + "\n";
            t.text += "<F4>: Time Scale: x" + Time.timeScale.ToString("F2") + "\n";
            t.text += "<F5>: Text Storage/Warp\n";
            t.text += "<F6>: VSync Count :" + QualitySettings.vSyncCount + "\n";
            t.text += "<F7><L>: Level Load: " + (!inVoid ? "void" : levelNames[currentLvlIdx]) + "\n";
            t.text += "<K>: Get All Keys\n";
            t.text += "<V>: Render Death Planes: " + (collisionRenderFlag ? "ON" : "OFF") + "\n";
            t.text += "<M>: Active Death Planes: " + (deathPlaneStatus ? "ON" : "OFF") + "\n";
            t.text += "<G>: Show NPC Talk Zone: " + (talkVolumeRenderFlag ? "ON" : "OFF") + "\n";
            t.text += "<R><noparse><B></noparse>: Warp Cam Move/Toggle \n";
            t.text += "<N>: Set Obj Transparent\n";
            t.text += "<T>: NoClip: " + (NoClipActive ? "ON" : "OFF") + "\n";
            // t.text += "<'>: Reset Camera Events\n";
            t.text += "<F11> Toggle UI\n\n";
            t.text += "Move Dir:" + PlayerMachine.moveDirection.ToString() + "\n";
            t.text += "Pos:" + PlayerMachine.transform.position + "\n";
            t.text += "SpawnPos:" + PlayerMachine.LastGroundLoc.ToString() + "\n";
            t.text += "Look Dir:" + PlayerMachine.lookDirection.ToString() + "\n";
            t.text += "Player State:" + PlayerMachine.currentState.ToString() + "\n";

            if(bossController)
            {
                t.text += "\nBoss Health: " + bossController.Health + "\n";
                t.text += "Boss State: " + bossStates[(int)BossController.State] + "\n";
            }
            if (warpCam.enabled)
            {
                t.text += "\nLGP:" + PlayerMachine.controller.LastGroundPos.ToString() + "\n";
                t.text += "LGO:" + PlayerMachine.controller.LastGroundOffset.ToString() + "\n";
                t.text += "LGR:" + PlayerMachine.controller.LastGroundRot.ToString() + "\n";
                t.text += "CG:" + PlayerMachine.controller.currentGround.transform.position.ToString() + "\n";
                t.text += "CR:" + PlayerMachine.controller.currentGround.transform.rotation.ToString() + "\n";
                t.text += "Warp Pos:" + warpSimPos.ToString() + "\n";
            }
        }
    }
}

