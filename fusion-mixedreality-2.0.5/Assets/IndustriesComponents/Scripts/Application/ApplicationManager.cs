using Fusion.Addons.Avatar;
using Fusion.XR.Shared;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using Photon.Voice.Fusion;
using System.Collections;
using TMPro;
using UnityEngine;

/**
* 
* The ApplicationManager is in charge to display error messages when an error occurs.
* So, the application manager listens the SessionEventsManager to get informed when a network error occurs.
* 
**/

namespace Fusion.Samples.IndustriesComponents
{

    public class ApplicationManager : ApplicationLifeCycleManager
    {
        public Managers managers;
        public SessionEventsManager sessionEventsManager;
        public GameObject staticLevel;
        public GameObject interactableObjects;
        const string shutdownErrorMessage = "Look like we have a connection issue.";
        const string disconnectedFromServerErrorMessage = "Sorry, you have been disconnected ! \n\n Please restart the application.";

        [SerializeField] private Material materialForOfflineHands;

        [SerializeField] private RigInfo _rigInfo;

        RigInfo RigInfo
        {
            get
            {
                if (_rigInfo == null)
                {
                    _rigInfo = RigInfo.FindRigInfo(managers.runner);
                }
                return _rigInfo;
            }
        }

        [Header("Desktop Rig settings")]
        public GameObject desktopErrorMessageGO;
        public TextMeshPro desktopErrorMessageTMP;

        [Header("XR Rig settings")]
        public GameObject hardwareRigErrorMessageGO;
        public TextMeshPro hardwareRigErrorMessageTMP;

        // Set to true to warn that we are disconnecting on purpose, preventing any unneeded warnings
        public bool isQuitting = false;

        // Set the reference to left & right hardware hand
        private HardwareHandRepresentationManager _leftHardwareHandRepresentationManager;
        private HardwareHandRepresentationManager _rightHardwareHandRepresentationManager;
        HardwareHandRepresentationManager LeftHardwareHandRepresentationManager
        {
            get
            {
                if (_leftHardwareHandRepresentationManager == null)
                {
                    if (RigInfo && RigInfo.localHardwareRig)
                    {
                        _leftHardwareHandRepresentationManager = RigInfo.localHardwareRig.leftHand.GetComponentInChildren<HardwareHandRepresentationManager>();
                    }
                }
                return _leftHardwareHandRepresentationManager;
            }
        }
        HardwareHandRepresentationManager RightHardwareHandRepresentationManager
        {
            get
            {
                if (_rightHardwareHandRepresentationManager == null)
                {
                    if (RigInfo && RigInfo.localHardwareRig)
                    {
                        _rightHardwareHandRepresentationManager = RigInfo.localHardwareRig.rightHand.GetComponentInChildren<HardwareHandRepresentationManager>();
                    }
                }
                return _rightHardwareHandRepresentationManager;
            }
        }

        private void Start()
        {
            if (managers == null) managers = Managers.FindInstance();
            if (managers) managers.applicationManager = this;

            if (!RigInfo)
                Debug.LogError("RigInfo not found !");

            if (!LeftHardwareHandRepresentationManager)
                Debug.LogWarning("LeftHardwareHandRepresentationManager not found. It is normal in Desktop mode.");

            if (!RightHardwareHandRepresentationManager)
                Debug.LogWarning("RightHardwareHandRepresentationManager not found. It is normal in Desktop mode.");

            if (!sessionEventsManager)
                sessionEventsManager = managers.runner.GetComponentInChildren<SessionEventsManager>();
            if (!sessionEventsManager)
                Debug.LogError("Can not get SessionEventsManager");

            sessionEventsManager.onDisconnectedFromServer.AddListener(DisconnectedFromServer);
            sessionEventsManager.onShutdown.AddListener(Shutdown);

        }

        private void OnDestroy()
        {
            sessionEventsManager.onDisconnectedFromServer.RemoveListener(DisconnectedFromServer);
            sessionEventsManager.onShutdown.RemoveListener(Shutdown);
        }

        // ShutdownWithError is called when the application is launched without an active network connection (network interface disabled or no link for example) or if an network interface failure occurs at run
        private void Shutdown(ShutdownReason shutdownReason)
        {
            if (isQuitting) return;
            Debug.LogError($" ApplicationManager Shutdown : { shutdownReason} ");
            string details = shutdownReason.ToString();
            if (details == "Ok") details = "Connection lost";
            // The runner will be destroyed, as we launch a coroutine, we want to survive :)
            transform.parent = null;
            
            // Display local hardware Rig
            DisplayLocalHardawareRig();

            UpdateErrorMessage(shutdownErrorMessage + $"\n\nCause: {details}");
            StartCoroutine(CleanUpScene());

        }

        // DisconnectedFromServer is called when the internet connection is lost.
        private void DisconnectedFromServer()
        {
            // Display local hardware Rig
            DisplayLocalHardawareRig();

            UpdateErrorMessage(disconnectedFromServerErrorMessage);
            StartCoroutine(CleanUpScene());
        }

        // DestroyNetworkedObjects is called when the connection erros occurs in order to delete spawned objects
        private void DestroyNetworkedObjects()
        {
            // Destroy the runner to delete Network objects (bots)
            if (managers.runner)
            {
                // restore the Unity Physics engine if the runner must be destroyed because it can not handle the physic anymore
                Debug.LogError("TODO : restore the Unity Physics when DestroyNetworkedObjects");
              /*  if (managers.runner.Config.PhysicsEngine != NetworkProjectConfig.PhysicsEngines.None)
                {
                    Physics.autoSimulation = true;
                }
              */
                var fusionVoiceClient = managers.runner.GetComponent<FusionVoiceClient>();
                if (fusionVoiceClient != null)
                    Destroy(fusionVoiceClient);

                GameObject.Destroy(managers.runner);
            }
        }

        // UpdateErrorMessage update the error message on the UI
        private void UpdateErrorMessage(string shutdownErrorMessage)
        {
            Debug.LogError($"UpdateErrorMessage : { shutdownErrorMessage} ");
            if (desktopErrorMessageTMP) desktopErrorMessageTMP.text = shutdownErrorMessage;
            if (hardwareRigErrorMessageTMP) hardwareRigErrorMessageTMP.text = shutdownErrorMessage;
        }

        // DisplayErrorMessage is in charge to hide all scene objects and display the error message
        private IEnumerator CleanUpScene()
        {
            GameObject errorGO = null;
            Fader fader = null;


            if (_rigInfo.localHardwareRigKind == RigInfo.RigKind.Desktop)
            {
                errorGO = desktopErrorMessageGO;
                fader = _rigInfo.localHardwareRig.headset.fader;
            }
            if (_rigInfo.localHardwareRigKind == RigInfo.RigKind.VR)
            {
                errorGO = hardwareRigErrorMessageGO;
                fader = _rigInfo.localHardwareRig.headset.fader;
            }

            yield return Fadeout(fader);
            ConfigureSceneForOfflineMode();
            yield return DisplayErrorMessage(fader, errorGO);

        }

        private IEnumerator Fadeout(Fader fader)
        {
            // display black screen
            if (fader) yield return fader.FadeIn();
            yield return new WaitForSeconds(1);
        }

        void ConfigureSceneForOfflineMode()
        {

            // Hide all scene
            HideScene();
            // Destroy spawned objects
            DestroyNetworkedObjects();

        }

        private void DisplayLocalHardawareRig()
        {
 
            if (LeftHardwareHandRepresentationManager != null && LeftHardwareHandRepresentationManager.localRepresentation != null)
            {
                Debug.Log("DisplayLocalHardawareRig : set LeftHand Material");
                LeftHardwareHandRepresentationManager.localRepresentation.SetHandMaterial(materialForOfflineHands);
            }

            if (RightHardwareHandRepresentationManager != null && RightHardwareHandRepresentationManager.localRepresentation != null)
                {
                Debug.Log("DisplayLocalHardawareRig : set Rigthand Material");
                RightHardwareHandRepresentationManager.localRepresentation.SetHandMaterial(materialForOfflineHands);
            }
        }

        private IEnumerator DisplayErrorMessage(Fader fader, GameObject errorMessage)
        {
            // Display error message UI
            if (errorMessage) errorMessage.SetActive(true);
            // remove black screen
            if (fader) yield return fader.FadeOut();
        }

        // HideScene is in charge to hide the scene 
        private void HideScene()
        {
            if (staticLevel) staticLevel.SetActive(false);
            if (interactableObjects) interactableObjects.SetActive(false);
            RenderSettings.skybox = null;
        }


        // QuitApplication is called when the user push the UI Exit button 
        public void QuitApplication()
        {
            Debug.LogError("User exit the application");
            Application.Quit();
        }

        public override void OnApplicationQuitRequest()
        {
            isQuitting = true;
            Destroy(this);
        }
    }
}