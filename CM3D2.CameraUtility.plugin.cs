// CM3D2CameraUtility.Plugin v2.0.1.2 改変の改変（非公式)
// 改変元 したらば改造スレその5 >>693
// http://pastebin.com/NxzuFaUe

// 20160103
// ・FPSモードでのカメラブレの補正機能追加
// ・VIPでのFPSモード有効化
// ・UIパネルを非表示にできるシーンの拡張
// 　(シーンレベル15)

// ■カメラブレ補正について
// Fキー(デフォルトの場合)を一回押下でオリジナルのFPSモード、もう一回押下でブレ補正モード。
// 再度押下でFPSモード解除。

// FPSモードの視点は男の頭の位置にカメラがセットされますが、
// 男の動きが激しい体位では視線がガクガクと大きく揺れます。
// 新しく追加したブレ補正モードではこの揺れを小さく抑えます。
// ただし男の目の位置とカメラ位置が一致しなくなるので、男の透明度を上げていると
// 体位によっては男の胴体の一部がちらちらと映り込みます。
// これの改善のため首の描画を消そうと思いましたが、男モデルは「頭部」「体」の2種類しか
// レンダリングされていないようで無理っぽかった。
// 気になる人は男の透明度を下げてください。


// CM3D2CameraUtility.Plugin v2.0.1.2 改変（非公式)
// Original by k8PzhOFo0 (https://github.com/k8PzhOFo0/CM3D2CameraUtility.Plugin)

using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2CameraUtility
{
    [PluginFilter("CM3D2x64"),
    PluginFilter("CM3D2x86"),
    PluginFilter("CM3D2VRx64"),
    PluginFilter("CM3D2OHx64"),
    PluginName("Camera Utility"),
    PluginVersion("2.0.1.2-20160220")]
    public class CameraUtility : PluginBase
    {
        //移動関係キー設定
        private KeyCode bgLeftMoveKey = KeyCode.LeftArrow;
        private KeyCode bgRightMoveKey = KeyCode.RightArrow;
        private KeyCode bgForwardMoveKey = KeyCode.UpArrow;
        private KeyCode bgBackMoveKey = KeyCode.DownArrow;
        private KeyCode bgUpMoveKey = KeyCode.PageUp;
        private KeyCode bgDownMoveKey = KeyCode.PageDown;
        private KeyCode bgLeftRotateKey = KeyCode.Delete;
        private KeyCode bgRightRotateKey = KeyCode.End;
        private KeyCode bgLeftPitchKey = KeyCode.Insert;
        private KeyCode bgRightPitchKey = KeyCode.Home;
        private KeyCode bgInitializeKey = KeyCode.Backspace;

        //VR用移動関係キー設定
        private KeyCode bgLeftMoveKeyVR = KeyCode.J;
        private KeyCode bgRightMoveKeyVR = KeyCode.L;
        private KeyCode bgForwardMoveKeyVR = KeyCode.I;
        private KeyCode bgBackMoveKeyVR = KeyCode.K;
        private KeyCode bgUpMoveKeyVR = KeyCode.Alpha0;
        private KeyCode bgDownMoveKeyVR = KeyCode.P;
        private KeyCode bgLeftRotateKeyVR = KeyCode.U;
        private KeyCode bgRightRotateKeyVR = KeyCode.O;
        private KeyCode bgLeftPitchKeyVR = KeyCode.Alpha8;
        private KeyCode bgRightPitchKeyVR = KeyCode.Alpha9;
        private KeyCode bgInitializeKeyVR = KeyCode.Backspace;

        //カメラ操作関係キー設定
        private KeyCode cameraLeftPitchKey = KeyCode.Period;
        private KeyCode cameraRightPitchKey = KeyCode.Backslash;
        private KeyCode cameraPitchInitializeKey = KeyCode.Slash;
        private KeyCode cameraFoVPlusKey = KeyCode.RightBracket;

        //Equalsになっているが日本語キーボードだとセミコロン
        private KeyCode cameraFoVMinusKey = KeyCode.Equals;

        //Semicolonになっているが日本語キーボードだとコロン
        private KeyCode cameraFoVInitializeKey = KeyCode.Semicolon;

        //こっち見てキー設定
        private KeyCode eyetoCamToggleKey = KeyCode.G;
        private KeyCode eyetoCamChangeKey = KeyCode.T;

        //夜伽UI消しキー設定
        private KeyCode hideUIToggleKey = KeyCode.Tab;

        //FPSモード切替キー設定
        private KeyCode cameraFPSModeToggleKey = KeyCode.F;

        private enum modKey
        {
            Shift,
            Alt,
            Ctrl
        }

        private Maid maid;
        private CameraMain mainCamera;
        private Transform mainCameraTransform;
        private Transform maidTransform;
        private Transform bg;
        private GameObject manHead;
        private GameObject uiObject;

        private float defaultFOV = 35f;
        private bool allowUpdate = false;
        private bool occulusVR = false;
        private bool chubLip = false;
        private bool fpsMode = false;
        private bool eyetoCamToggle = false;

        private float cameraRotateSpeed = 1f;
        private float cameraFOVChangeSpeed = 0.25f;
        private float floorMoveSpeed = 0.05f;
        private float maidRotateSpeed = 2f;
        private float fpsModeFoV = 60f;

        private int sceneLevel;
        private int frameCount = 0;

        private float fpsOffsetForward = 0.02f;
        private float fpsOffsetUp = -0.06f;
        private float fpsOffsetRight = 0f;

        ////以下の数値だと男の目の付近にカメラが移動しますが
        ////うちのメイドはデフォで顔ではなく喉元見てるのであんまりこっち見てくれません
        //private float fpsOffsetForward = 0.1f;
        //private float fpsOffsetUp = 0.12f;

        private Vector3 oldPos;
        private Vector3 oldTargetPos;
        private float oldDistance;
        private float oldFoV;
        private Quaternion oldRotation;

        private bool oldEyetoCamToggle;
        private int eyeToCamIndex = 0;

        private bool uiVisible = true;
        private GameObject profilePanel;

        //20160103 FPSカメラブレ補正用
        private Vector3 cameraOffset = Vector3.zero;
        private bool bFpsShakeCorrection = false;
        //20160103 ここまで

        public void Awake()
        {
            GameObject.DontDestroyOnLoad(this);

            string path = Application.dataPath;
            chubLip = path.Contains("CM3D2OHx64");
            occulusVR = path.Contains("CM3D2VRx64");
            
            if (occulusVR)
            {
                bgLeftMoveKey = bgLeftMoveKeyVR;
                bgRightMoveKey = bgRightMoveKeyVR;
                bgForwardMoveKey = bgForwardMoveKeyVR;
                bgBackMoveKey = bgBackMoveKeyVR;
                bgUpMoveKey = bgUpMoveKeyVR;
                bgDownMoveKey = bgDownMoveKeyVR;
                bgLeftRotateKey = bgLeftRotateKeyVR;
                bgRightRotateKey = bgRightRotateKeyVR;
                bgLeftPitchKey = bgLeftPitchKeyVR;
                bgRightPitchKey = bgRightPitchKeyVR;
                bgInitializeKey = bgInitializeKeyVR;
            }
        }

        public void Start()
        {
            mainCameraTransform = Camera.main.gameObject.transform;
        }

        public void OnLevelWasLoaded(int level)
        {
            sceneLevel = level;

            maid = GameMain.Instance.CharacterMgr.GetMaid(0);

            if (maid)
            {
                maidTransform = maid.body0.transform;
            }

            bg = GameObject.Find("__GameMain__/BG").transform;

            mainCamera = GameMain.Instance.MainCamera;

            if (maid && bg && maidTransform)
            {
                allowUpdate = true;
            }
            else
            {
                allowUpdate = false;
            }

            if (occulusVR)
            {
                uiObject = GameObject.Find("ovr_screen");
            }
            else
            {
                uiObject = GameObject.Find("/UI Root/Camera");
                //20160103
                if (uiObject == null)
                    uiObject = GameObject.Find("SystemUI Root/Camera");
                //20160103 ここまで
                defaultFOV = Camera.main.fieldOfView;
            }

            if (level == 5)
            {
                GameObject uiRoot = GameObject.Find("/UI Root");
                profilePanel = uiRoot.transform.Find("ProfilePanel").gameObject;
            }
            else if (level == 12)
            {
                GameObject uiRoot = GameObject.Find("/UI Root");
                profilePanel = uiRoot.transform.Find("UserEditPanel").gameObject;
            }
            //20160103
            else if (level == 15)
            {
                //profilePanelが何をしているのかよくわからないので適当
                GameObject uiRoot = GameObject.Find("__GameMain__/SystemUI Root");
                profilePanel = uiRoot.transform.Find("ConfigPanel").gameObject;
            }
            cameraOffset = Vector3.zero;
            bFpsShakeCorrection = false;
            //20160103 ここまで

            fpsMode = false;
    }

        private bool getModKeyPressing(modKey key)
        {
            switch (key)
            {
                case modKey.Shift:
                    return (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

                case modKey.Alt:
                    return (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

                case modKey.Ctrl:
                    return (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));

                default:
                    return false;
            }
        }

        private void SaveCameraPos()
        {
            oldPos = mainCamera.GetPos();
            oldTargetPos = mainCamera.GetTargetPos();
            oldDistance = mainCamera.GetDistance();
            oldRotation = mainCameraTransform.rotation;
            oldFoV = Camera.main.fieldOfView;
        }

        private void LoadCameraPos()
        {
            mainCameraTransform.rotation = oldRotation;
            mainCamera.SetPos(oldPos);
            mainCamera.SetTargetPos(oldTargetPos, true);
            mainCamera.SetDistance(oldDistance, true);
            Camera.main.fieldOfView = oldFoV;
        }

        private Vector3 GetYotogiPlayPosition()
        {
            var field = mainCamera.GetType().GetField("m_vCenter", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            return (Vector3)field.GetValue(mainCamera);
        }

        private void FirstPersonCamera()
        {
            if ((chubLip && sceneLevel == 10) || sceneLevel == 14 || sceneLevel == 15 || sceneLevel == 24)
            {
                if (!manHead)
                {
                    if (frameCount == 60)
                    {
                        GameObject manExHead = GameObject.Find("__GameMain__/Character/Active/AllOffset/Man[0]");
                        Transform[] manExHeadTransforms = manExHead ? manExHead.GetComponentsInChildren<Transform>() : new Transform[0];

                        Transform[] manHedas = manExHeadTransforms.Where(trans => trans.name.IndexOf("_SM_") > -1).ToArray();

                        foreach (Transform mh in manHedas)
                        {
                            GameObject smManHead = mh.gameObject;
                            foreach (Transform smmh in smManHead.transform)
                            {
                                if (smmh.name.IndexOf("ManHead") > -1)
                                {
                                    manHead = smmh.gameObject;
                                }
                            }
                        }
                        frameCount = 0;
                    }
                    else
                    {
                        frameCount++;
                    }
                }
                else
                {
                    if (occulusVR)
                    {
                        if (Input.GetKeyDown(cameraFPSModeToggleKey))
                        {
                            //    eyetoCamToggle = false;
                            //    maid.EyeToCamera(Maid.EyeMoveType.無し, 0f);
                            Vector3 localPos = uiObject.transform.localPosition;
                            mainCamera.SetPos(manHead.transform.position);
                            uiObject.transform.position = manHead.transform.position;
                            uiObject.transform.localPosition = localPos;
                        }
                    }
                    else
                    {
                        if (Input.GetKeyDown(cameraFPSModeToggleKey))
                        {
                            //20160103
                            if (bFpsShakeCorrection)
                            {
                                bFpsShakeCorrection = false;
                                fpsMode = false;
                                Console.WriteLine("FpsMode = Disable");
                            }
                            else if(fpsMode && !bFpsShakeCorrection)
                            {
                                bFpsShakeCorrection = true;
                                Console.WriteLine("FpsMode = Enable : ShakeCorrection = Enable");
                            }
                            else
                            {
                                fpsMode = true;
                                SaveCameraPos();
                                Console.WriteLine("FpsMode = Enable : ShakeCorrection = Disable");
                            }
                            //20160103 ここまで
                            //修正前コード
                            //fpsMode = !fpsMode;
                            //Console.WriteLine("fpsmode = " + fpsMode.ToString());

                            if (fpsMode)
                            {
                                //20160103
                                //SaveCameraPos();

                                Camera.main.fieldOfView = fpsModeFoV;
                                eyetoCamToggle = false;
                                maid.EyeToCamera(Maid.EyeMoveType.無し, 0f);

                                mainCameraTransform.rotation = Quaternion.LookRotation(-manHead.transform.up);

                                manHead.renderer.enabled = false;
                            }
                            else
                            {
                                Vector3 cameraTargetPosFromScript = GetYotogiPlayPosition();

                                if (oldTargetPos != cameraTargetPosFromScript)
                                {
                                    Console.WriteLine("Position Changed!");
                                    oldTargetPos = cameraTargetPosFromScript;
                                }
                                manHead.renderer.enabled = true;

                                LoadCameraPos();
                                eyetoCamToggle = oldEyetoCamToggle;
                                oldEyetoCamToggle = eyetoCamToggle;
                            }
                        }
                        if (fpsMode)
                        {
                            Vector3 cameraTargetPosFromScript = GetYotogiPlayPosition();
                            if (oldTargetPos != cameraTargetPosFromScript)
                            {
                                Console.WriteLine("Position Changed!");
                                mainCameraTransform.rotation = Quaternion.LookRotation(-manHead.transform.up);
                                oldTargetPos = cameraTargetPosFromScript;
                            }

                            //20160103
                            Vector3 CameraPos = manHead.transform.position + manHead.transform.up * fpsOffsetUp + manHead.transform.right * fpsOffsetRight + manHead.transform.forward * fpsOffsetForward;
                            if (bFpsShakeCorrection)
                            {
                                cameraOffset = Vector3.Lerp(CameraPos, cameraOffset, 0.9f);
                                mainCamera.SetPos(cameraOffset);
                                mainCamera.SetTargetPos(cameraOffset, true);
                                mainCamera.SetDistance(0f, true);
                            }
                            else
                            {
                                mainCamera.SetPos(CameraPos);
                                mainCamera.SetTargetPos(CameraPos, true);
                                mainCamera.SetDistance(0f, true);
                            }
                            //20160103 ここまで
                            //修正前コード
                            //mainCamera.SetPos(manHead.transform.position + manHead.transform.up * fpsOffsetUp + manHead.transform.right * fpsOffsetRight + manHead.transform.forward * fpsOffsetForward);
                            //mainCamera.SetTargetPos(manHead.transform.position + manHead.transform.up * fpsOffsetUp + manHead.transform.right * fpsOffsetRight + manHead.transform.forward * fpsOffsetForward, true);
                            //mainCamera.SetDistance(0f, true);
                        }
                    }
                }
            }
        }

        private void ExtendedCameraHandle()
        {
            if (!occulusVR)
            {
                if (mainCameraTransform)
                {
                    if (Input.GetKey(cameraFoVMinusKey))
                    {
                        Camera.main.fieldOfView += -cameraFOVChangeSpeed;
                    }
                    if (Input.GetKey(cameraFoVInitializeKey))
                    {
                        Camera.main.fieldOfView = defaultFOV;
                    }
                    if (Input.GetKey(cameraFoVPlusKey))
                    {
                        Camera.main.fieldOfView += cameraFOVChangeSpeed;
                    }
                    if (Input.GetKey(cameraLeftPitchKey))
                    {
                        mainCameraTransform.Rotate(0, 0, cameraRotateSpeed);
                    }
                    if (Input.GetKey(cameraPitchInitializeKey))
                    {
                        mainCameraTransform.eulerAngles = new Vector3(
                            mainCameraTransform.rotation.eulerAngles.x,
                            mainCameraTransform.rotation.eulerAngles.y,
                            0f);
                    }
                    if (Input.GetKey(cameraRightPitchKey))
                    {
                        mainCameraTransform.Rotate(0, 0, -cameraRotateSpeed);
                    }
                }
            }
        }

        private void FloorMover(float moveSpeed, float rotateSpeed)
        {
            if (bg)
            {
                Vector3 cameraForward = mainCameraTransform.TransformDirection(Vector3.forward);
                Vector3 cameraRight = mainCameraTransform.TransformDirection(Vector3.right);
                Vector3 cameraUp = mainCameraTransform.TransformDirection(Vector3.up);

                Vector3 direction = Vector3.zero;

                if (Input.GetKey(bgLeftMoveKey))
                {
                    direction += new Vector3(cameraRight.x, 0f, cameraRight.z) * moveSpeed;
                }
                if (Input.GetKey(bgRightMoveKey))
                {
                    direction += new Vector3(cameraRight.x, 0f, cameraRight.z) * -moveSpeed;
                }
                if (Input.GetKey(bgBackMoveKey))
                {
                    direction += new Vector3(cameraForward.x, 0f, cameraForward.z) * moveSpeed;
                }
                if (Input.GetKey(bgForwardMoveKey))
                {
                    direction += new Vector3(cameraForward.x, 0f, cameraForward.z) * -moveSpeed;
                }
                if (Input.GetKey(bgUpMoveKey))
                {
                    direction += new Vector3(0f, cameraUp.y, 0f) * -moveSpeed;
                }
                if (Input.GetKey(bgDownMoveKey))
                {
                    direction += new Vector3(0f, cameraUp.y, 0f) * moveSpeed;
                }

                //bg.position += direction;
                bg.localPosition += direction;

                if (Input.GetKey(bgLeftRotateKey))
                {
                    bg.RotateAround(maidTransform.transform.position, Vector3.up, rotateSpeed);
                }
                if (Input.GetKey(bgRightRotateKey))
                {
                    bg.RotateAround(maidTransform.transform.position, Vector3.up, -rotateSpeed);
                }
                if (Input.GetKey(bgLeftPitchKey))
                {
                    bg.RotateAround(maidTransform.transform.position, new Vector3(cameraForward.x, 0f, cameraForward.z), rotateSpeed);
                }
                if (Input.GetKey(bgRightPitchKey))
                {
                    bg.RotateAround(maidTransform.transform.position, new Vector3(cameraForward.x, 0f, cameraForward.z), -rotateSpeed);
                }

                if (getModKeyPressing(modKey.Alt) && (Input.GetKey(bgLeftRotateKey) || Input.GetKey(bgRightRotateKey)))
                {
                    bg.RotateAround(maidTransform.position, Vector3.up, -bg.rotation.eulerAngles.y);
                }
                if (getModKeyPressing(modKey.Alt) && (Input.GetKey(bgLeftPitchKey) || Input.GetKey(bgRightPitchKey)))
                {
                    bg.RotateAround(maidTransform.position, Vector3.forward, -bg.rotation.eulerAngles.z);
                    bg.RotateAround(maidTransform.position, Vector3.right, -bg.rotation.eulerAngles.x);
                }
                if (getModKeyPressing(modKey.Alt) && (Input.GetKey(bgLeftMoveKey) || Input.GetKey(bgRightMoveKey) || Input.GetKey(bgBackMoveKey) || Input.GetKey(bgForwardMoveKey)))
                {
                    bg.localPosition = new Vector3(0f, bg.localPosition.y, 0f);
                }
                if (getModKeyPressing(modKey.Alt) && (Input.GetKey(bgUpMoveKey) || Input.GetKey(bgDownMoveKey)))
                {
                    bg.localPosition = new Vector3(bg.localPosition.x, 0f, bg.localPosition.z);
                }
                if (Input.GetKeyDown(bgInitializeKey))
                {
                    bg.localPosition = Vector3.zero;
                    bg.RotateAround(maidTransform.position, Vector3.up, -bg.rotation.eulerAngles.y);
                    bg.RotateAround(maidTransform.position, Vector3.right, -bg.rotation.eulerAngles.x);
                    bg.RotateAround(maidTransform.position, Vector3.forward, -bg.rotation.eulerAngles.z);
                    bg.RotateAround(maidTransform.position, Vector3.up, -bg.rotation.eulerAngles.y);
                }
            }
        }

        private void LookAtThis()
        {
            if (Input.GetKeyDown(eyetoCamChangeKey))
            {
                if (eyeToCamIndex == Enum.GetNames(typeof(Maid.EyeMoveType)).Length - 1)
                {
                    eyetoCamToggle = false;
                    eyeToCamIndex = 0;
                }
                else
                {
                    eyeToCamIndex++;
                    eyetoCamToggle = true;
                }
                maid.EyeToCamera((Maid.EyeMoveType)eyeToCamIndex, 0f);
                Console.WriteLine("EyeToCam:{0}", eyeToCamIndex);
            }

            if (Input.GetKeyDown(eyetoCamToggleKey))
            {
                eyetoCamToggle = !eyetoCamToggle;
                //Console.WriteLine("Eye to Cam : {0}", eyetoCamToggle);
                if (!eyetoCamToggle)
                {
                    maid.EyeToCamera(Maid.EyeMoveType.無し, 0f);
                    eyeToCamIndex = 0;
                    Console.WriteLine("EyeToCam:{0}", eyeToCamIndex);
                }
                else
                {
                    maid.EyeToCamera(Maid.EyeMoveType.目と顔を向ける, 0f);
                    eyeToCamIndex = 5;
                    Console.WriteLine("EyeToCam:{0}", eyeToCamIndex);
                }
            }
        }

        private void HideUI()
        {
            if (Input.GetKeyDown(hideUIToggleKey))
            {
                //20160103
                if (sceneLevel == 5 || sceneLevel == 14 || sceneLevel == 15)
                //20160103 ここまで
                //修正前コード
                //if (sceneLevel == 5 || sceneLevel == 14)
                {
                    var field = GameMain.Instance.MainCamera.GetType().GetField("m_eFadeState", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);

                    int i = (int)field.GetValue(mainCamera);
                    //Console.WriteLine("FadeState:{0}", i);
                    if (i == 0)
                    {
                        uiVisible = !uiVisible;
                        if (uiObject)
                        {
                            uiObject.SetActive(uiVisible);
                        }
                    }
                    Console.WriteLine("UIVisible:{0}", uiVisible);
                }
            }
        }

        public void Update()
        {
            if (sceneLevel == 5)
            {
                if (profilePanel.activeSelf)
                {
                    allowUpdate = false;
                }
                else
                {
                    allowUpdate = true;
                }
            }
            else if (sceneLevel == 12)
            {
                if (profilePanel.activeSelf)
                {
                    allowUpdate = false;
                }
                else
                {
                    allowUpdate = true;
                }
            }
            //20160103
            else if (sceneLevel == 15)
            {
                if (profilePanel.activeSelf)
                {
                    allowUpdate = false;
                }
                else
                {
                    allowUpdate = true;
                }
            }
            //20160103 ここまで

            if (allowUpdate)
            {
                float moveSpeed = floorMoveSpeed;
                float rotateSpeed = maidRotateSpeed;

                if (getModKeyPressing(modKey.Shift))
                {
                    moveSpeed *= 0.1f;
                    rotateSpeed *= 0.1f;
                }

                FirstPersonCamera();

                LookAtThis();

                FloorMover(moveSpeed, rotateSpeed);

                if (!occulusVR)
                {
                    ExtendedCameraHandle();

                    HideUI();
                }
            }
        }
    }
}