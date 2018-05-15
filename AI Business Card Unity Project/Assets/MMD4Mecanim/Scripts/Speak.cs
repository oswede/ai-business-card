using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Speak : MonoBehaviour
{
    public AudioSource ttsSource;
    private AudioClip audio_clip = null;
    public Toggle toggle;
    // Use this for initialization
    void Start()
    {
       // MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
        Animator anim = this.GetComponent<Animator>();
        anim.SetBool("Stand", true);
        anim.SetInteger("Talking", 0);
       

    }

    // Update is called once per frame
    void Update()
    {
        var audio_source = this.GetComponent<AudioSource>();
        Debug.Log(toggle.isOn);
        if (toggle.isOn)
        {
            audio_source.mute = false;
        }
        else if (!toggle.isOn)
        {
            audio_source.mute = true;
        }
        MMD4MecanimSpeechHelper test = this.GetComponent<MMD4MecanimSpeechHelper>();
        var lipSync = this.GetComponent<MMD4M_LipSync>();
        Animator anim = this.GetComponent<Animator>();
        //var Model = this.GetComponent<MMD4MecanimModel>();
        
        //lipSync.volume = 0.0f;
        int rangeRadomNum = 0;
        
        if (ttsSource.clip == audio_clip && ttsSource.isPlaying)
        {
            UnityEngine.Random a = new UnityEngine.Random();
            System.Random b = new System.Random();
            rangeRadomNum = Random.Range(1, 3);

            anim.SetBool("Stand", false);
            anim.SetInteger("Talking", rangeRadomNum);
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("talking2_vmd"))
            {
                anim.SetBool("Stand", true);
                anim.SetInteger("Talking", 0);
            }
            if (info.IsName("talking1_vmd"))
            {
                anim.SetBool("Stand", true);
                anim.SetInteger("Talking", 0);
            }

        }
        else if (ttsSource.clip != audio_clip)
        {
            lipSync.Play(ttsSource.clip);
            lipSync.volume = 1.0f;
        }
        audio_clip = ttsSource.clip;
    }
 
}
