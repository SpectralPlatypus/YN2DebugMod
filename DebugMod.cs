using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "6.6";
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
                    LogDebug("Found Spectral!");
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
                            "%n10%v0%\r\nSpectral\r\n" +
                            "%m1%When is the %m0%%s.5%%sD%%p15%%e1%%e2%Android%p10%%m1% port getting released anyway?\r\n\r\n%n\r\n";
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
