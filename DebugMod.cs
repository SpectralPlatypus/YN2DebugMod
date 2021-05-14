using Pepperoni;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod
{
    public class DebugMod : Mod
    {
        private const string _modVersion = "8.2";
        private DebugHUD modHUD = null;
        private static GameObject go = null;
        private readonly Vector3 npcPos = new Vector3(926f, 44f, 364.7f);
        private float npcDistance = 0.0f;
        public DebugMod() : base("DebugMod")
        {
        }

        public override string GetVersion() => _modVersion;

        public override void Initialize()
        {
            go = new GameObject();
            modHUD = go.AddComponent<DebugHUD>();
            Object.DontDestroyOnLoad(go);

            SceneManager.activeSceneChanged += OnSceneChange;
            ModHooks.Instance.OnParseScriptHook += Instance_OnParseScriptHook;
            On.PlayerMachine.Jump_SuperUpdate += PlayerMachine_Jump_SuperUpdate;
            On.HiddenPlatform.Activate += HiddenPlatform_Activate;
        }

        private void HiddenPlatform_Activate(On.HiddenPlatform.orig_Activate orig, HiddenPlatform self)
        {
            bool temp = self.flag;
            orig(self);
            if (modHUD.CamEventReset && temp) self.flag = false;
        }

        //For noclip fun
        private void PlayerMachine_Jump_SuperUpdate(On.PlayerMachine.orig_Jump_SuperUpdate orig, PlayerMachine self)
        {
            if (modHUD.NoClipActive)
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
                modHUD.ToggleState(true, playerMachine);
            }
            else
            {
                if (modHUD) modHUD.ToggleState(false, null);
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
                            "%m1%Run%p11% %m0%%s.1%A%p8%l%p9%l %p10%%m1%N%p15%P%p16%Cs%p17%%sD%%p10%!\r\n\r\n%n\r\n";
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
