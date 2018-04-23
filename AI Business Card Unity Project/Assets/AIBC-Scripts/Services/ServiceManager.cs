using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

    /**
     * This script controls the interaction between the different services, by sending the requests and managing the sequence of events in accordance with the application's functionality.
     * Each service has its own handler script, with each connection being established as soon as the application is opened. In this way, each service_handler does not interact 
     * directly with other services, allowing for later reusability if the requirements change.
     * 
     * Instead of calling GameObject.GetComponent on each of the member variables, they are all referenced from within the Unity interface (this slightly improves its performance), 
     * and need to be declared public for this.
     */

public class ServiceManager : MonoBehaviour, 
    convo_handler.ConvoResponse, stt_handler.STTResponse, tts_handler.TTSResponse
{

    public stt_handler stt;
    public convo_handler convo;
    public tts_handler tts;

    public AudioSource ttsAudioSource;

    public Text speakIndicator;
    public Text convo_output_display;

    void Start () {
        speakIndicator.text = "Stop Talking";
        speakIndicator.color = new Color32(189, 0, 0, 0xFF);

        stt.setCallback(this); // set stt's callback
        convo.setCallback(this); // set convo's callback
        tts.setCallback(this); // set tts's callback
    }

    public void sttResponseReceived(string lastSttResponse)
    {
        stt.StopLogging();  // stop stt from logging

        speakIndicator.text = "Stop Talking";
        speakIndicator.color = new Color32(189, 0, 0, 0xFF);

        string stt_output = lastSttResponse; // fetch last message
        convo.Message(stt_output);
    }

    public void convoResponseReceived(string lastConvoResponse)
    {
        convo_output_display.text = lastConvoResponse;
        tts.Synthesize(lastConvoResponse);
    }

    public void setConvoSubtitlesEnabled(bool subtitlesEnabled)
    {
        convo_output_display.enabled = subtitlesEnabled;
    }

    public void ttsResponseReceived(AudioClip lastTtsResponse)
    {
        PlayClip(lastTtsResponse);
        StartCoroutine(WaitForClip(lastTtsResponse, 3));
    }

    public void setAudioEnabled(bool audioEnabled)
    {
        ttsAudioSource.mute = !audioEnabled;
    }
    
    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {
            ttsAudioSource.clip = clip;
            ttsAudioSource.Play();
        }
    }

    IEnumerator WaitForClip(AudioClip clip, float pause)
    {
        yield return new WaitForSecondsRealtime(clip.length + pause);   //Wait
        stt.StartLogging();
        speakIndicator.text = "Start Talking";
        speakIndicator.color = new Color32(0, 104, 0, 0xFF);
    }
    
}