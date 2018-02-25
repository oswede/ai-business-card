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

    // public Text convo_output_display;

	void Start () {
        // initialise conversation
        // initialise text to speech
        // initisalise speech to text

        stt.StartRecording();

        //tts2 = GameObject.Find("tts_handler").GetComponent<tts_handler>();
        //tts2.Synthesize();
    }
	
	// Update is called once per frame
	void Update () {
		
        if (stt.hasNextSttResponse()) // check to see if the final response has been received each frame. It automatically stops recording immediately if this is the case.
        {
            stt.waitForNextSttResponse(); // set it to false immediately after
            stt.StopRecording();
            string stt_output = stt.getSttOutput(); // fetch last message
            convo.Message(stt_output);
        }

        if (convo.hasNextConvoResponse())
        {
            convo.waitForNextConvoResponse(); // set to false immediately after
            tts.Synthesize(convo.getLastConvoOutput());
        }

	}
}
