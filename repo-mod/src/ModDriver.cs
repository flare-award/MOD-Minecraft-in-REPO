// Last-resort ticker.
//
// In R.E.P.O. the plugin component never receives Update()/OnGUI() (BepInEx's
// manager object is alive, active and enabled, yet Unity delivers no messages to
// it). Everything therefore runs from the plugin's own render pump
// (Application.onBeforeRender). OnGUI, however, can only be drawn from an
// OnGUI() callback, so the pump creates one of these on a fresh GameObject:
// objects created after startup are ticked normally by the game.
//
// If this driver does receive Update()/OnGUI(), it takes over and the pump
// steps back (the plugin guards per-frame work with Time.frameCount).

using UnityEngine;

namespace MinecraftInRepo
{
    public sealed class ModDriver : MonoBehaviour
    {
        private MinecraftInRepoPlugin owner;

        public void Init(MinecraftInRepoPlugin plugin)
        {
            owner = plugin;
        }

        private void Update()
        {
            if (owner != null)
            {
                owner.TickFromDriver();
            }
        }

        private void OnGUI()
        {
            if (owner != null)
            {
                owner.DrawFromDriver();
            }
        }
    }
}
