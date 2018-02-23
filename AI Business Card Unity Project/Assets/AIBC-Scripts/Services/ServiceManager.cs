using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServiceManager : MonoBehaviour {

    // Each service has its own handler script. This script provide the functionality for interacting between the different service handlers
    // It prevents other game components from having to interact with the individual service handlers directly.

    public stt_handler stt;
    public convo_handler convo;
    public tts_handler tts;

	void Start () {
        // initialise conversation
        // initialise text to speech
        // initisalise speech to text

        convo.Message(null); // send initial null message to the conversation to get the first response
        stt.StartRecording();
	}
	
	// Update is called once per frame
	void Update () {
		
        if (stt.hasNextFinalResponse()) // each frame, check to see if the final response has been received
        {
            stt.waitForNextFinalResponse();
            stt.StopRecording();
            string stt_output = stt.getSttOutput(); // fetch last message
            convo.Message(stt_output);
        }

	}
}
