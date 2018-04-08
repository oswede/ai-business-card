using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Connection;

public class tts_handler : MonoBehaviour {

    private TextToSpeech _textToSpeech;

    private string tts_username = "05fb2b07-352a-42cd-8fa3-23869014194a";
    private string tts_password = "LMDKiiChSd7x";
    private string tts_url = "https://stream.watsonplatform.net/text-to-speech/api";

    private AudioClip lastClip;

    private bool isPlaying;

    private TTSResponse callback;

    void Start () {
        LogSystem.InstallDefaultReactors();

        Credentials credentials = new Credentials(tts_username, tts_password, tts_url);
        _textToSpeech = new TextToSpeech(credentials);

        _textToSpeech.Voice = VoiceType.en_US_Michael;

        //_textToSpeech.AudioFormat = AudioFormatType.FLAC; // lossless but compressed - currently not working

        isPlaying = false;
    }

    public void Synthesize(string input)
    {
        if (!_textToSpeech.ToSpeech(OnSynthesize, OnFail, input))
            Log.Debug("TTS.ToSpeech()", "Failed to synthesize!");
    }

    private void OnSynthesize(AudioClip clip, Dictionary<string, object> customData)
    {
        lastClip = clip;
        callback.ttsResponseReceived(lastClip);
        //PlayClip(clip); //now handled by ServiceManager
    }

    private void OnFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("TTS.OnFail()", "Error received: {0}", error.ToString());
    }

    public AudioClip getLastTtsResponse()
    {
        return lastClip;
    }

    public void setCallback(ServiceManager newCallback)
    {
        callback = newCallback;
    }

    public interface TTSResponse
    {
        void ttsResponseReceived(AudioClip lastResponse); // show output and move on to next stage
    }

}