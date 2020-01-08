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
        private bool _enabled = false;
        private float yaw = 0f;
        private float pitch = 0f;

        private List<NPCCache> dialogues = new List<NPCCache>(10);
        private List<GameObject> collisionPlaneCache = new List<GameObject>(5);
        private Texture2D glassTexture = null;
        private Camera warpCam = null;
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
                if (fn.Contains("glass_texture"))
                {
                    using (Stream imageStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fn))
                    {
                        byte[] imageBuffer = new byte[imageStream.Length];
                        imageStream.Read(imageBuffer, 0, imageBuffer.Length);
                        imageStream.Flush();
                        glassTexture = new Texture2D(1, 1);
                        glassTexture.LoadImage(imageBuffer);
                        Logger.LogDebug("Loaded Glass Texture");
                    }
                    break;
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
                    // Remove unused and/or broken NPC dialogues -- thanks Denise
                    if (npcName == string.Empty || npcName == "Denise") continue;
                    dialogues.Add(new NPCCache(npcName, textAsset));
                }
            }
        }

        private void BuildCollisionCache()
        {
            collisionPlaneCache.Clear();
            var deathPlanes = FindObjectsOfType<VoidOut>();
            foreach (var d in deathPlanes)
            {
                var collObj = d.GetComponent<Collider>();
                Logger.LogDebug("Found Collider of type " + collObj.GetType().Name);

                if (!(collObj is BoxCollider) && !(collObj is MeshCollider))
                    continue;

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
                    go.transform.position = d.transform.position;
                    go.transform.localScale = meshCol.transform.localScale;
                    go.GetComponent<MeshFilter>().mesh = meshCol.sharedMesh;
                    go.GetComponent<MeshRenderer>().material.color = Color.grey;
                }

                go.GetComponent<Collider>().enabled = false;
                go.GetComponent<MeshRenderer>().receiveShadows = false;
                collisionPlaneCache.Add(go);
            }
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
            PlayerMachine = (_enabled) ? playerMachine : null;

            if (_enabled)
            {
                BuildDialogueCache();
                collisionRenderFlag = false;
            }
        }

        private static readonly int MAX_COSTUME_INDEX = 3;
        private static string[] costumeNames = { "Noid", "Green", "Sanic", "Cappy" };
        private static string[] levelNames = { "LeviLevle", "dungeon", "PZNTv5" };
        private int currentCostIdx = 0;
        private int currentNpcIdx = 0;
        private int currentLvlIdx = 0;
        private float deltaTime = 0f;
        private bool _visible = false;
        private bool deathPlaneStatus = true;
        private bool collisionRenderFlag = false;
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
                foreach (var tv in FindObjectsOfType<TalkVolume>())
                {
                    if (tv.gameObject.GetComponent<LineRenderer>())
                        continue;

                    var line = tv.gameObject.AddComponent<LineRenderer>();
                    var collider = tv.gameObject.GetComponent<Collider>();
                    if (collider is SphereCollider)
                    {
                        float radius = (collider as SphereCollider).radius;
                        line.useWorldSpace = false;
                        line.startWidth = .1f;
                        line.endWidth = .1f;
                        line.positionCount = 360 + 1;
                        var pointCount = 360 + 1; // add extra point to make startpoint and endpoint the same to close the circle
                        var points = new Vector3[pointCount];
                        for (int i = 0; i < pointCount; i++)
                        {
                            var rad = Mathf.Deg2Rad * (i * 360f / 360);
                            points[i] = new Vector3(Mathf.Sin(rad) * radius, 0, Mathf.Cos(rad) * radius);
                        }

                        line.SetPositions(points);
                    }
                }
            }
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
                {
                    BuildCollisionCache();
                }
                foreach (var c in collisionPlaneCache)
                {
                    if (c == null) break;
                    c.SetActive(collisionRenderFlag);
                }
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

            if (Input.GetKeyDown(KeyCode.N))
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
                            matl.SetTexture("_MainTex", glassTexture);
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
            t.text += "<R><noparse><B></noparse>: Warp Cam Move/Toggle \n";
            t.text += "<N>: Set Obj Transparent\n";
            t.text += "<G>: Show NPC Talk Zone\n";
            t.text += "<F11> Toggle UI\n\n";
            t.text += "Move Dir:" + PlayerMachine.moveDirection.ToString() + "\n";
            t.text += "Pos:" + PlayerMachine.transform.position + "\n";
            t.text += "SpawnPos:" + PlayerMachine.LastGroundLoc.ToString() + "\n";
            t.text += "Look Dir:" + PlayerMachine.lookDirection.ToString() + "\n";
            t.text += "Player State:" + PlayerMachine.currentState.ToString() + "\n";

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

