using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "7.8";
        private DebugHUD counter = null;
        private static GameObject go = null;
        private readonly Vector3 npcPos = new Vector3(926f, 44f, 364.7f);
        private float npcDistance = 0.0f;
        public DebugMod() : base("DebugMod")
        {
        }

        public override string GetVersion() => _modVersion;

        public override void Initialize()
        {
            SceneManager.activeSceneChanged += OnSceneChange;
            ModHooks.Instance.OnParseScriptHook += Instance_OnParseScriptHook;
            On.PlayerMachine.Jump_SuperUpdate += PlayerMachine_Jump_SuperUpdate;

            go = new GameObject();
            counter = go.AddComponent<DebugHUD>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        //For noclip fun
        private void PlayerMachine_Jump_SuperUpdate(On.PlayerMachine.orig_Jump_SuperUpdate orig, PlayerMachine self)
        {
            if(DebugHUD.NoClipActive)
            {
                self.Gravity = 0f;
                orig(self);
                self.VerticalVelocity = Vector3.zero;
                if (Kueido.Input.Jump.Held)
                {
                    self.transform.position += self.controller.up / 8;
                }
                else if (Input.GetKey(KeyCode.LeftControl))
                {
                    self.transform.position -= self.controller.up / 8;
                }
            }
            else
            {
               orig(self);
            }
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
                        var collider = npc.GetComponentInChildren<Collider>();
                        if (collider)
                        {
                            npcDistance = collider.bounds.size.x;
                        }
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
                        Vector3.Distance(npcPos, playerPos) <= npcDistance)
                    {
                        output =
                            "%n10%v0%\r\nSpectral\r\n" +
                            "%m1%Run %m0%%s.5%%sD%%p15%%e1%%e2%All%p10%%m1% NPCs!\r\n\r\n%n\r\n";
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
