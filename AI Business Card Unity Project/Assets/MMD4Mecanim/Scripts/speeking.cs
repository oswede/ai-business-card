using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class speeking : MonoBehaviour {

	// Use this for initialization
	void Start () {
		MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();

	}
	
	// Update is called once per frame
	void Update () {

        /*
		MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
		if (test.isProcessing) {

		} else {
			test.speechMorphText = lastInput;
		}
        */
		
	}

    public void updateInput(string newInput)
    {
        MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
        test.speechMorphText = newInput;
    }


}
