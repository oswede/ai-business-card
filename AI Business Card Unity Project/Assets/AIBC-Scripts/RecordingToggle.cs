using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecordingToggle : MonoBehaviour {

    public Text statusText;
    private bool isRecording;

    public AudioClip input;

    public SpeechToText_Handler s2t;

	void Start () {
        statusText = GetComponentInChildren<Text>();
        isRecording = false; // not recording initially
        this.GetComponent<Button>().onClick.AddListener(OnChange);

        //input = GameObject.Find("Audio Source").GetComponent<AudioSource>();
        s2t = GameObject.Find("SpeechToText_Handler").GetComponent<SpeechToText_Handler>();
       // s2t = new SpeechToText_Handler();
    }

void OnChange()
    {
        isRecording = !isRecording; // change recording state
        
        // update text according to the new state
        if (isRecording) // recording has resumed, so button should display 'stop recording'
        {
            input = Microphone.Start("Microphone", false, 5, 44100);

            statusText.text = "Stop Recording";
            statusText.fontSize = 27;
        } else // is currently not recording, so button should display 'start recording'
        {
            Microphone.End("Microphone");

            statusText.text = "Start Recording";
            statusText.fontSize = 30;
            Debug.Log("pre-convertion");
            // pass the audio source to the speech to text engine
            s2t.Convert(input);

        }
        
    }

    void Update()
    {
        if (isRecording && !Microphone.IsRecording("Microphone")) // if recording is set to true, but the microphone has stopped recording as the loop has finished, update
        {
            OnChange();
        }
    }

}

/*
 *  // Non-streaming
 SpeechToText m_SpeechToText = new SpeechToText();
 AudioClip m_AudioClip = new AudioClip();
 m_SpeechToText.Recognize(m_AudioClip, OnRecognize);
 //  Streaming
 m_SpeechToText.StartListening(OnRecognize);
 //  Stop listening
 m_SpeechToText.StopListening();
 //  Callback for the listen functions.
 private void OnRecognize(SpeechResultList result)
 {
 foreach( var res in result.Results )
     {
         foreach( var alt in res.Alternatives )
         {
             string text = alt.Transcript;
             Debug.Log(string.Format( "{0} ({1}, {2:0.00})\n", text, res.Final ? "Final" : "Interim", alt.Confidence));
         }
     }
 } 

    */