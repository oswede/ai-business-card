using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class speeking : MonoBehaviour {
	public AudioSource ttsSource;
	// Use this for initialization
	void Start () {
		MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
		Animator anim = this.GetComponent<Animator> ();
		anim.SetBool ("Stand", true);
		anim.SetInteger ("Talking", 0);
		var Model = this.GetComponent<MMD4MecanimModel> ();


	}
	
	// Update is called once per frame
	void Update () {
		MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
		var lipSync = this.GetComponent<MMD4M_LipSync> ();
		Animator anim = this.GetComponent<Animator> ();
		var Model = this.GetComponent<MMD4MecanimModel> ();



		int rangeRadomNum = 0;

		if (lipSync.isTalking) {
			UnityEngine.Random a = new UnityEngine.Random();
			System.Random b = new System.Random();
			rangeRadomNum = Random.Range(1, 3);

			anim.SetBool ("Stand", false);
			anim.SetInteger ("Talking",rangeRadomNum);
			AnimatorStateInfo info =anim.GetCurrentAnimatorStateInfo(0);
			if (info.IsName("talking2_vmd"))
			{
				anim.SetBool ("Stand", true);
				anim.SetInteger ("Talking", 0);
			}
			if (info.IsName("talking1_vmd"))
			{
				anim.SetBool ("Stand", true);
				anim.SetInteger ("Talking", 0);
			}

		} else {
			anim.SetBool ("Stand", true);
			anim.SetInteger ("Talking", 0);
			if (ttsSource.clip == null) {
			}
			else
			lipSync.Play (ttsSource.clip);

		}
		
	}
}
