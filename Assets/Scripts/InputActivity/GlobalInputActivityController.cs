using System;using UnityEngine;namespace Mojinloop.InputActivity{[DefaultExecutionOrder(-900)]public sealed class GlobalInputActivityController:MonoBehaviour{public event Action ActivityDetected;IGlobalInputActivitySource source;void OnEnable(){
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
source=new WindowsGlobalInputActivitySource();
#else
source=new EditorInputActivitySource();
#endif
source.ActivityDetected+=Forward;try{source.StartListening();}catch(Exception e){Debug.LogError(e.Message);}}void Update(){if(source is WindowsGlobalInputActivitySource w)w.Poll();}void Forward()=>ActivityDetected?.Invoke();void OnDisable(){if(source==null)return;source.ActivityDetected-=Forward;source.StopListening();source=null;}void OnApplicationQuit()=>OnDisable();}}
