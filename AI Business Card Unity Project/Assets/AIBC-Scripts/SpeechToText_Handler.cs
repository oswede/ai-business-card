using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Connection;
using UnityEngine.UI;

public class SpeechToText_Handler : MonoBehaviour {

    private string _username = "8f137eeb-cb17-47d4-afea-36b21f6982f0";
    private string _password = "tMawRjCtV56K";
    private string _url = "https://stream.watsonplatform.net/speech-to-text/api";

    private SpeechToText _speechToText;

    public Text display;
    
	void Start () {

        // start credentials etc.
        LogSystem.InstallDefaultReactors();

        Credentials credentials = new Credentials(_username, _password, _url);

        _speechToText = new SpeechToText(credentials);

        display = GameObject.Find("conversationOutputText").GetComponent<Text>();

        display.text = "";
	}
	
    public void Convert(AudioClip input)
    {
        Debug.Log("just before recognition");
        //SavWav.Save("myfile", input.clip);
        _speechToText.Recognize(HandleSuccess, HandleFail, input);
        Debug.Log("after recognition");
    }

    private void HandleSuccess(SpeechRecognitionEvent result, Dictionary<string, object> textInput)
    {
        
        Debug.Log("here");
        
        string res = textInput["json"].ToString();
        Log.Debug("ExampleSpeechToText.HandleSuccess()", "Speech to Text - Get model response: {0}", res);
 
        //Debug.Log(res);

        ResultData r = JsonUtility.FromJson<ResultData>(res);

        display.text = r.results[0].alternatives[0].transcript;
        //display.text += test;
        
        /*
        Output m_ResultOutput = new Output();

        m_ResultOutput.SendData(new SpeechToTextData(result));

        if (result != null && result.results.Length > 0)
        {
            if (m_Transcript != null)
                m_Transcript.text = "";

            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    string text = alt.transcript;

                    if (m_Transcript != null)
                        m_Transcript.text += string.Format("{0} ({1}, {2:0.00})\n",
                            text, res.final ? "Final" : "Interim", alt.confidence);
                }
            }

*/

        }

    private void HandleFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("ExampleAlchemyLanguage.OnFail()", "Error received: {0}", error.ToString());
    }

	// Update is called once per frame
	void Update () {
		
	}
}
