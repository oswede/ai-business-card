using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RecordingToggle : MonoBehaviour {

    public Text statusText;
    private bool isRecording;

    public AudioSource input;

    public ServiceTest testing;

    void Start() {
        statusText = GetComponentInChildren<Text>();
        isRecording = false; // not recording initially

        testing = GameObject.Find("ServiceTest").GetComponent<ServiceTest>();

        this.GetComponent<Button>().onClick.AddListener(OnChange);

        // input = GameObject.Find("Audio Source").GetComponent<AudioSource>();

    }

    void OnChange()
    {
        Debug.Log("here");
        isRecording = !isRecording; // change recording state

        // update text according to the new state
        if (isRecording) // recording has resumed, so button should display 'stop recording'
        {
            statusText.text = "Stop Recording";
            statusText.fontSize = 27;

            testing.Go();
        }
        else // is currently not recording, so button should display 'start recording'
        {
            testing.StopRecording();

            statusText.text = "Start Recording";
            statusText.fontSize = 30;
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


        /*Microphone.End("Microphone");

        statusText.text = "Start Recording";
        statusText.fontSize = 30;
        Debug.Log("pre-convertion");
        // pass the audio source to the speech to text engine
        s2t.Convert(input.clip);
        /*
        //string lastTextFromSpeech = s2t.getLastTextFromSpeech();
        //display.text = lastTextFromSpeech;
        //csn.sendNextQuestion(display.text);*/