using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ibc.console
{
    public class IbcConsole : MonoBehaviour
    {
        public class LogData
        {
            public string Data;
            public int Level;

            public LogData(string data, int level)
            {
                Data = data;
                Level = level;
            }
        }

        [SerializeField] private short _maximumLogInstances = 100;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private GameObject _visual;
        [SerializeField] private GameObject _logPrefab;

        private readonly Queue<GameObject> _logInstancesQueue = new Queue<GameObject>();
        private readonly Queue<LogData> _logDataQueue = new Queue<LogData>();
        private bool _isVisible;

        private static IbcConsole _instance;
        public static IbcConsole GetInstance()
        {
            if (_instance == null)
                _instance = FindObjectOfType<IbcConsole>();
            if (_instance == null)
                _instance = Instantiate(Resources.Load<GameObject>("IbcConsole")).GetComponent<IbcConsole>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Application.logMessageReceived += LogCallback;

            GetInstance();
            DontDestroyOnLoad(gameObject);
        }


        private void LogCallback(string condition, string stackTrace, LogType type)
        {
            Publish(0, condition);
        }

        private void Update()
        {

            if (_isVisible)
            {
                while (_logDataQueue.Count > 0)
                {
                    var logData = _logDataQueue.Dequeue();
                    var logInstance = CreateConsoleLog();
                    logInstance.Publish(logData);
                }
            }
            else
            {
            }
        }

        public void Toggle()
        {
            if (_isVisible) Hide(); else Show();
        }

        public void Show()
        {
            _isVisible = true;
            _visual.SetActive(true);
        }

        public void Hide()
        {
            _isVisible = false;
            _visual.SetActive(false);
        }

        public void Publish(string msg)
        {
            Publish(0, msg);
        }

        public void Publish(int logLevel, string msg)
        {
            _logDataQueue.Enqueue(new LogData(msg, logLevel));
            Show();
        }


        public void Dispose()
        {
            foreach (var li in _logInstancesQueue)
                Destroy(li);
            _logInstancesQueue.Clear();
            _logDataQueue.Clear();
        }

        private IbcConsoleLog CreateConsoleLog()
        {
            var logObj = Instantiate(_logPrefab, _spawnPoint);
            var logInstance = logObj.GetComponent<IbcConsoleLog>();
            _logInstancesQueue.Enqueue(logObj);

            if (_logInstancesQueue.Count > _maximumLogInstances)
            {
                var li = _logInstancesQueue.Dequeue();
                Destroy(li.gameObject);
            }

            return logInstance;
        }
    }
}