using StarterAssets;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

[RequireComponent(typeof(PlayableDirector))]
[DefaultExecutionOrder(-10)]
public class FreezeRevealCutscene : MonoBehaviour
{
    [SerializeField] private WaterFreezer freezer;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private StarterAssetsInputs playerInputs;
    [Tooltip("Movement, interaction, spell and element input components paused during the reveal.")]
    [SerializeField] private Behaviour[] controls;
    [SerializeField] private Canvas[] hudCanvases;
    [SerializeField] private CinemachineCamera droneCamera;
    [SerializeField, Min(0.1f)] private float returnDuration = 0.75f;

    private PlayableDirector director;
    private bool played;
    private bool playing;
    private bool[] controlStates;
    private bool[] canvasStates;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private float cameraFieldOfView;
    private bool freezeFinished;
    private float returnElapsed;
    private bool preview;

    public bool IsPlaying => playing;
    public bool CanPlay => !played && !playing;

    private void Awake() => director = GetComponent<PlayableDirector>();

    private void OnEnable()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        director.stopped += OnStopped;
        if (freezer != null)
        {
            freezer.OnFreezeStarted.AddListener(PlayReveal);
            freezer.OnFreezeFinished.AddListener(OnFreezeFinished);
        }
    }

    private void OnDisable()
    {
        if (freezer != null)
        {
            freezer.OnFreezeStarted.RemoveListener(PlayReveal);
            freezer.OnFreezeFinished.RemoveListener(OnFreezeFinished);
        }
        if (director != null)
        {
            director.stopped -= OnStopped;
            if (playing) director.Stop();
        }
        RestorePlayer();
    }

    public void PlayReveal() => StartPlayback(false);
    public void PlayPreview() => StartPlayback(true);

    private void StartPlayback(bool isPreview)
    {
        if (played || playing) return;
        if (director == null) director = GetComponent<PlayableDirector>();
        if (director.playableAsset == null || playerCamera == null || playerInputs == null || droneCamera == null || (!isPreview && freezer == null))
        {
            Debug.LogError("FreezeRevealCutscene needs its Timeline, player camera, inputs, freezer and drone camera assigned.", this);
            return;
        }
        if (director.duration <= returnDuration)
        {
            Debug.LogError("FreezeRevealCutscene Timeline must be longer than its return blend.", this);
            return;
        }

        played = playing = true;
        preview = isPreview;
        freezeFinished = false;
        returnElapsed = 0f;
        cameraLocalPosition = playerCamera.transform.localPosition;
        cameraLocalRotation = playerCamera.transform.localRotation;
        cameraFieldOfView = playerCamera.fieldOfView;
        controlStates = new bool[controls.Length];
        canvasStates = new bool[hudCanvases.Length];
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] == null) continue;
            controlStates[i] = controls[i].enabled;
            controls[i].enabled = false;
        }
        for (int i = 0; i < hudCanvases.Length; i++)
        {
            if (hudCanvases[i] == null) continue;
            canvasStates[i] = hudCanvases[i].enabled;
            hudCanvases[i].enabled = false;
        }
        ClearInput();
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        if (!preview) FollowFreeze();
        director.time = 0;
        director.Play();
    }

    private void OnFreezeFinished() => freezeFinished = true;

    private void LateUpdate()
    {
        if (!playing) return;
        if (preview)
        {
            director.time = System.Math.Min(director.duration, director.time + Time.deltaTime);
            director.Evaluate();
            if (director.time >= director.duration) director.Stop();
            return;
        }
        FollowFreeze();
        double flightEnd = director.duration - returnDuration;
        if (freezeFinished)
        {
            returnElapsed += Time.deltaTime;
            director.time = System.Math.Min(director.duration, flightEnd + returnElapsed);
        }
        else director.time = freezer.Progress * flightEnd;
        director.Evaluate();
        if (freezeFinished && returnElapsed >= returnDuration) director.Stop();
    }

    private void FollowFreeze()
    {
        // shortcut: follows the current unrotated Plane's X axis; update the path and UV mapping if the river is rotated.
        var bounds = freezer.IceBounds;
        float softness = freezer.EdgeSoftness;
        float front = Mathf.Clamp01(Mathf.Lerp(-softness, 1f + softness, freezer.Progress));
        float x = Mathf.Lerp(bounds.max.x, bounds.min.x, front);
        droneCamera.transform.position = new Vector3(Mathf.Min(x + 65f, bounds.max.x), bounds.center.y + 35f, bounds.max.z - 25f);
        droneCamera.transform.rotation = Quaternion.LookRotation(new Vector3(-65f, -35f, 0f), Vector3.up);
    }

    private void OnStopped(PlayableDirector stoppedDirector) => RestorePlayer();

    private void RestorePlayer()
    {
        if (!playing) return;
        playing = false;
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = cameraLocalPosition;
            playerCamera.transform.localRotation = cameraLocalRotation;
            playerCamera.fieldOfView = cameraFieldOfView;
        }
        for (int i = 0; i < controls.Length; i++)
            if (controls[i] != null) controls[i].enabled = controlStates[i];
        for (int i = 0; i < hudCanvases.Length; i++)
            if (hudCanvases[i] != null) hudCanvases[i].enabled = canvasStates[i];
        ClearInput();
    }

    private void ClearInput()
    {
        if (playerInputs == null) return;
        playerInputs.move = playerInputs.look = Vector2.zero;
        playerInputs.jump = playerInputs.sprint = false;
    }
}
