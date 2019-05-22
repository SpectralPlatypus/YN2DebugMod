using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "4.5";
        private DebugHUD counter = null;
        private static GameObject go = null;
        private readonly Vector3 chantroPos = new Vector3(926f, 44f, 364.7f);

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
            LogDebug("New Scene Name: " + newScene.name);

            var playerMachine = Manager.Player.GetComponent<PlayerMachine>();
            if (playerMachine)
            {
                counter.ToggleState(true, playerMachine);
            }
            else
            {
                if(counter) counter.ToggleState(false, null);
            }

            if(newScene.name == "void")
            {
                Basic_NPC chantro = null;
                var npcs = UnityEngine.Object.FindObjectsOfType<Basic_NPC>();

                foreach (var npc in npcs)
                {
                    var textAsset = npc.GetComponentInChildren<TalkVolume>().Dialogue;
                    if (DialogueUtils.GetNPCName(textAsset.text) == "Chantro")
                    {
                        chantro = npc;
                        break;
                    }
                }
                if (chantro != null)
                {
                    LogDebug("Found Chantro!");
                    UnityEngine.Object.Instantiate(chantro, chantroPos, Quaternion.identity);
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
            if (DialogueUtils.GetNPCName(text) != "Chantro" || Vector3.Distance(chantroPos, playerPos) > 5)
            {
                return text;
            }

            return "%n9%v1%\r\nSpectral\r\n%m1%Have some fun with%m0%%s1% %m1%%sD%Warps!!\r\n\r\n%n\r\n";
        }
    }
}
