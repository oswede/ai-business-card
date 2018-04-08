using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ServiceManager : MonoBehaviour, 
    convo_handler.ConvoResponse, stt_handler.STTResponse, tts_handler.TTSResponse
{

    // Each service has its own handler script. This script provide the functionality for interacting between the different service handlers
    // It prevents other game components from having to interact with the individual service handlers directly.

    public stt_handler stt; // component referenced in scene
    public convo_handler convo; // component referenced in scene
    public tts_handler tts; // component referenced in scene

    public AudioSource ttsAudioSource;

    private bool loggingResumed;

    public Text speakIndicator;

    double timer = 0.0; // begins at this value
    double timerMax = 3.0; // event occurs at this value

    //public AudioClip lastClip;
    //public Text convo_output_display;

   void Start () {
        speakIndicator.text = "Stop Talking";
        speakIndicator.color = new Color32(189, 0, 0, 0xFF);

        stt.setCallback(this); // set stt's callback
        convo.setCallback(this); // set convo's callback
        tts.setCallback(this); // set tts's callback
    }

    public void sttResponseReceived(string lastResponse)
    {
        stt.StopLogging();  // stop stt from logging

        speakIndicator.text = "Stop Talking";
        speakIndicator.color = new Color32(189, 0, 0, 0xFF);

        string stt_output = lastResponse; // fetch last message
        convo.Message(stt_output);
    }

    public void convoResponseReceived(string lastResponse)
    {
        tts.Synthesize(lastResponse);
    }

    public void ttsResponseReceived(AudioClip lastResponse)
    {
        PlayClip(tts.getLastTtsResponse());
        
        StartCoroutine(WaitThenPause(lastResponse, 3));
        
        
    }

    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {
            ttsAudioSource.clip = clip;
            ttsAudioSource.Play();
        }

    }
    
    IEnumerator WaitThenPause(AudioClip clip, float pause)
    {
        yield return new WaitForSecondsRealtime(clip.length + pause);   //Wait

        stt.StartLogging();
        speakIndicator.text = "Start Talking";
        speakIndicator.color = new Color32(0, 104, 0, 0xFF);

    }

}