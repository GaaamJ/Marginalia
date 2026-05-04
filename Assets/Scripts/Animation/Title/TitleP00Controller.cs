using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// P00 타이틀 화면.
/// 화면 클릭 시 Canvas 비활성화 후 TitleSceneController.OnP00Complete() 호출.
///
/// [Inspector 연결]
///   titleSceneController : TitleSceneController
///   p00Canvas            : P00 Canvas GameObject (클릭 시 비활성화)
/// </summary>
public class TitleP00Controller : MonoBehaviour
{
    [SerializeField] private TitleSceneController titleSceneController;
    [SerializeField] private GameObject p00Canvas;
    [SerializeField] private MMF_Player fadeIn;

    private bool _done = false;

    private void Start()
    {
        if (fadeIn != null)
            fadeIn.PlayFeedbacks();
    }

    private void Update()
    {
        if (_done) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            _done = true;
            p00Canvas?.SetActive(false);
            titleSceneController?.OnP00Complete();
        }
    }
}
