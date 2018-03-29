using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class blineyes : MonoBehaviour {

	private float nowtime = 3.0f;
	static float[] Weights = new float[] { 100, 75, 50, 25, 0 }; 

	bool m_enable = true;

    bool m_blinking;
    float m_timer;
    int m_index;



	void Start () {
		MMD4MecanimMorphHelper test = this.GetComponent<MMD4MecanimMorphHelper>();
		test.morphName = "まばたき";
		test.morphSpeed = 0.3f;
		test.morphWeight = 0.0f;



	}

	// Update is called once per frame
	void Update () {
		
		
		m_timer -= Time.deltaTime;

        if (m_blinking)
        {
            if (m_timer < 0)
            {
                m_timer = 0.05f;                    
                m_index++;

                if (m_index < Weights.Length)
                    SetShape(m_index);              
                else
                {
                    m_blinking = false;             
                    m_timer = 5.0f;   
                }
            }
        }
        else
        {
            if (m_timer < 0)
                ToBlink();                          // 开始眨眼
        }

	}

	void SetShape(int index)
    {
		MMD4MecanimMorphHelper test = this.GetComponent<MMD4MecanimMorphHelper>();
        test.morphWeight =  Weights[index];
    }
	
	void ToBlink()
    {
        m_blinking = true;
        m_timer = 0;
        m_index = -1;
    }

		

}
