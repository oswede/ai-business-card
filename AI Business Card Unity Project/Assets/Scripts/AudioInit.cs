using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioInit : MonoBehaviour {

    public AudioSource audioSource;
    public MMD4MecanimModel model;
    public float DalyTime = 0.2f;


    void Awake()
    {
        audioSource.Stop();
        audioSource.playOnAwake = false;
        model.DalyTime = DalyTime;
        StartCoroutine(InitAudio());
    }

    // Use this for initialization
    //void Start()
    //{
    //    //StartCoroutine(InitAudio());
    //}

    IEnumerator InitAudio()
    {
        while(false==model.isInit)
        {
            //print("Initing...->");
            yield return StartCoroutine(_print("Initing...->"));
            yield return 0;
        }
        if (false == audioSource.isPlaying)
        {
            audioSource.Play();
            yield return StartCoroutine(_print("Play Audio"));
            //print("Play Audio");
        }

    }

    IEnumerator _print(string msg)
    {
        print(msg);
        yield return 0;
    }

	//// Update is called once per frame
	//void Update () {
		
	//}
}
