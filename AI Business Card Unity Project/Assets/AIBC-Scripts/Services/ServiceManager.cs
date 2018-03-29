using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ServiceManager : MonoBehaviour {

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
        // automatically set: sst.StartRecording(), logging = false
        loggingResumed = false;
        //isWaiting = false;

        speakIndicator.text = "Stop Talking";
        speakIndicator.color = new Color32(189, 0, 0, 0xFF);
    }

    // Update is called once per frame
    void Update()
    {

        if (stt.hasNextSttResponse() && (timer == 0.0)) // check to see if the final response has been received each frame. It automatically stops recording immediately if this is the case.
        {
            Debug.Log("one");

            speakIndicator.text = "Stop Talking";
            speakIndicator.color = new Color32(189, 0, 0, 0xFF);

            stt.waitForNextSttResponse(); // set it responseReceived to false
            //stt.StopLogging(); // called within stt_handler when the response is received instead. Once the final response has been received, stop updating the last output.

            string stt_output = stt.getSttOutput(); // fetch last message
            convo.Message(stt_output);
        }
        else if (convo.hasNextConvoResponse())
        {
            Debug.Log("two");
            convo.waitForNextConvoResponse(); // set to false immediately after
            tts.Synthesize(convo.getLastConvoOutput());
        }
        else if (tts.hasNextTtsResponse())
        {
            Debug.Log("three");
            tts.waitForNextTtsResponse(); // set to false immediately after
            PlayClip(tts.getLastTtsResponse());
            //lastClip = tts.getLastTtsResponse();
            loggingResumed = false;
        }
        else if (!ttsAudioSource.isPlaying && (ttsAudioSource.clip != null) && !loggingResumed) // the service routine has finished, and the received clip has finished playing
        {

            if (timer == 0) Debug.Log("timer started");

            //Debug.Log("four");
            timer += Time.deltaTime;

            if (timer >= timerMax)
            {
                Debug.Log("timerMax reached !");

                stt.StartLogging();
                loggingResumed = true;
                speakIndicator.text = "Start Talking";
                speakIndicator.color = new Color32(0, 104, 0, 0xFF);

                timer = 0;

            }

        }

    }

    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {
            ttsAudioSource.clip = clip;
            ttsAudioSource.Play();
        }

    }
}