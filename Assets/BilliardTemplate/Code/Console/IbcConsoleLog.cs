
using UnityEngine;
using UnityEngine.UI;

namespace ibc.console
{

    public class IbcConsoleLog : MonoBehaviour
    {
        [SerializeField] private Text _msgLabel;

        public void Publish(IbcConsole.LogData logData)
        {
            _msgLabel.text = logData.Data;

            switch (logData.Level)
            {
                case 0:
                    _msgLabel.color = Color.white;
                    break;
                case -1:
                    _msgLabel.color = Color.red;
                    break;
            }
        }
    }
}