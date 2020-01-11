using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "6.5";
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
            ModHooks.Instance.OnParseScriptHook += Instance_OnParseScriptHook;
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
                var npcs = Object.FindObjectsOfType<Basic_NPC>();

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
                    Object.Instantiate(spectralNpc, npcPos, Quaternion.identity);
                }
            }
        }

        private string Instance_OnParseScriptHook(string text)
        {
            string output = text;
            switch (DialogueUtils.GetNPCName(text))
            {
                case "Oleia":
                    var playerPos = Manager.Player.GetComponent<PlayerMachine>().transform.position;
                    if (SceneManager.GetActiveScene().name == "void" &&
                        Vector3.Distance(npcPos, playerPos) <= 5f)
                    {
                        output =
                            "%n13%v11%\r\nSpectral\r\n%m1%Get me pictures of%m0%%s1% %m1%%sD%Spiderman!\r\n\r\n%n\r\n";
                    }
                    break;
                case "Denise":
                    output = text.Replace(@"%v14", @"%v6");
                    break;
            }

            return output;
        }
    }
}
