
using UnityEngine;

namespace ibc.controller.eightpool
{

    public class EightPoolInterfaceController : MonoBehaviour
    {
        [SerializeField]
        private EightPoolBilliard _eightPoolBilliard;

        private void OnGUI()
        {
            GUILayout.Space(52);
            GUILayout.Label($"Billiard state: {_eightPoolBilliard.CurrentGameState.ToString()}");
            GUILayout.Label($"Current player: {_eightPoolBilliard.ActivePlayerIndex.ToString()}");
        }

    }
}