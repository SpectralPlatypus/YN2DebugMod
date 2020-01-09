using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "6.2";
        private DebugHUD counter = null;
        private static GameObject go = null;
        private readonly Vector3 npcPos = new Vector3(926f, 44f, 364.7f);

        public DebugMod() : base("DebugMod")
        {
        }

        public override string GetVersion() => _modVersion;

        public override void Initialize()
        {
            SceneManager.activeSceneChanged += OnSceneChange;
            go = new GameObject();
            counter = go.AddComponent<DebugHUD>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private void OnSceneChange(Scene oldScene, Scene newScene)
        {
            var playerMachine = Manager.Player.GetComponent<PlayerMachine>();
            if (playerMachine)
            {
                counter.ToggleState(true, playerMachine);
            }
            else
            {
                if (counter) counter.ToggleState(false, null);
            }

            if (newScene.name == "void")
            {
                Basic_NPC spectralNpc = null;
                var npcs = UnityEngine.Object.FindObjectsOfType<Basic_NPC>();

                foreach (var npc in npcs)
                {
                    var textAsset = npc.GetComponentInChildren<TalkVolume>().Dialogue;
                    if (DialogueUtils.GetNPCName(textAsset.text) == "Oleia")
                    {
                        spectralNpc = npc;
                        break;
                    }
                }
                if (spectralNpc != null)
                {
                    LogDebug("Found Chantro!");
                    UnityEngine.Object.Instantiate(spectralNpc, npcPos, Quaternion.identity);
                    ModHooks.Instance.OnParseScriptHook += Instance_OnParseScriptHook;
                }
            }
            else
            {
                ModHooks.Instance.OnParseScriptHook -= Instance_OnParseScriptHook;
            }
        }

        private string Instance_OnParseScriptHook(string text)
        {
            var playerPos = Manager.Player.GetComponent<PlayerMachine>().transform.position;
            if (DialogueUtils.GetNPCName(text) != "Oleia" || Vector3.Distance(npcPos, playerPos) > 5)
            {
                return text;
            }

            return "%n13%v11%\r\nSpectral\r\n%m1%Get me pictures of%m0%%s1% %m1%%sD%Spiderman!\r\n\r\n%n\r\n";
        }
    }
}
