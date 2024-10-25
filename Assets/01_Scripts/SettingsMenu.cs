using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SettingsMenu : MonoBehaviour
{
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown fullscreenDropdown;
    public Slider bgmSlider;
    public Slider sfxSlider;

    private Resolution[] resolutions;

    void Start()
    {
        // �ػ� �ɼ� ����
        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        HashSet<string> uniqueResolutions = new HashSet<string>();

        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            if (uniqueResolutions.Add(option)) // �ߺ� �ػ� ����
            {
                options.Add(option);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = options.Count - 1;
                }
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // �ػ� ���� �̺�Ʈ ����
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        // ȭ�� ��� �ɼ� ����
        fullscreenDropdown.onValueChanged.AddListener(SetFullscreen);

        // BGM �� SFX �����̴� �ʱ�ȭ �� �̺�Ʈ ����
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
        bgmSlider.onValueChanged.AddListener(SetBGMVolume);

        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetResolution(int resolutionIndex)
    {
        string[] dimensions = resolutionDropdown.options[resolutionIndex].text.Split('x');
        int width = int.Parse(dimensions[0].Trim());
        int height = int.Parse(dimensions[1].Trim());
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }

    public void SetFullscreen(int mode)
    {
        switch (mode)
        {
            case 0: // â ���
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case 1: // ��ü ȭ�� ���
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case 2: // ������ â ���
                Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
                break;
        }
    }

    public void SetBGMVolume(float volume)
    {
        PlayerPrefs.SetFloat("BGMVolume", volume);
        AudioListener.volume = volume; // ����: ��ü ���� ����
        // ���� ���ӿ����� AudioMixer ����� �����մϴ�.
    }

    public void SetSFXVolume(float volume)
    {
        PlayerPrefs.SetFloat("SFXVolume", volume);
        // SFX ������ ��� �� ȿ���� ������Ʈ�� ������ AudioSource���� volume ���� �����ϴ� ������� ���� �����մϴ�.
    }

    public void CloseSettingUI()
    {
        gameObject.SetActive(false);
    }
}
