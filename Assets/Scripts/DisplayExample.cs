using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DisplayExample : MonoBehaviour
{
    public Button button;

    private JJDisplayManager displayManager;

    void Start()
    {
        displayManager = new JJDisplayManager();
        displayManager.open();
        Button btn = button.GetComponent<Button>();
        btn.onClick.AddListener(TaskOnClick);
    }
    void TaskOnClick(){
        if(displayManager.GetDisplayMode() == (int)JJDisplayManager.DisplayMode.DISPLAY_2D)
        {
            displayManager.SetDisplayMode(JJDisplayManager.DisplayMode.DISPLAY_3D);
        }else{
            displayManager.SetDisplayMode(JJDisplayManager.DisplayMode.DISPLAY_2D);
        }
    }
}
