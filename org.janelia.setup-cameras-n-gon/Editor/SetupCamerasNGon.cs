﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Depends on: org.janelia.camera-utilities for OffAxisPerspectiveCamera.

namespace Janelia
{
    // If an existing object is used for a screen, it is assumed to be a quad, aligned with the XY plane,
    // with unit size (i.e., going from -0.5 to 0.5).

    public class SetupCamerasNGon : EditorWindow
    {
        private GameObject _mouse;
        private string _mouseComponentExtra;

        [Serializable]
        private class CameraScreen
        {
            public Camera camera;
            public GameObject screen;
        }
        private List<CameraScreen> _cameraScreens;

        // The N in the N-gon is _numCameras + _numEmptySides.

        private int _numCameras = 3;
        private int _numEmptySides = 1;
        private float _screenWidth = 197.042f;
        private float _screenHeight = 147.782f;
        private float _fractionalHeight = 0.25f;
        private float _rotationY = -90f;
        private float _offsetX = 0;
        private float _offsetZ = 0;
        private float _near = 0.1f;
        private float _far = 1000.0f;

        // With GameObject.CreatePrimitive(), properties like these must be present somewhere
        // in the code to prevent a crash due to code stripping.  See the note at the bottom here:
        // https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html

        private MeshFilter _preventStrippingMeshFilter;
        private MeshRenderer _preventStrippingMeshRenderer;
        private BoxCollider _preventStrippingBoxCollider;

        [MenuItem("Window/Setup Cameras, N-gon")]
        public static void ShowWindow()
        {
            SetupCamerasNGon window = (SetupCamerasNGon)GetWindow(typeof(SetupCamerasNGon));
        }

        public SetupCamerasNGon()
        {
            _cameraScreens = new List<CameraScreen>();
            _rotationY = rotationYCentered();
        }

        public void OnEnable()
        {
            Load();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            _mouse = (GameObject)EditorGUILayout.ObjectField("Mouse", _mouse, typeof(GameObject), true);

            // A script listed here (without the ".cs" suffix) will be added to the created "mouse" object,
            // as a convenience.
            _mouseComponentExtra = EditorGUILayout.TextField("Extra script for mouse", _mouseComponentExtra);

            int numCamerasBefore = _numCameras;
            int numEmptySidesBefore = _numEmptySides;
            _numCameras = EditorGUILayout.IntField("Number of cameras", _numCameras);
            _numEmptySides = EditorGUILayout.IntField("Number of empty sides", _numEmptySides);

            _screenWidth = EditorGUILayout.FloatField("Screen width (mm)", _screenWidth);
            _screenHeight = EditorGUILayout.FloatField("Screen height (mm)", _screenHeight);
            _fractionalHeight = EditorGUILayout.FloatField("Fractional height", _fractionalHeight);

            _rotationY = EditorGUILayout.FloatField("Rotation Y (deg)", _rotationY);

            // The created "mouse" object will be displaced from the center of the n-gon by this vector.
            _offsetX = EditorGUILayout.FloatField("Offset X (mm)", _offsetX);
            _offsetZ = EditorGUILayout.FloatField("Offset Z (mm)", _offsetZ);

            _near = EditorGUILayout.FloatField("Near", _near);
            _far = EditorGUILayout.FloatField("Far", _far);

            while (_cameraScreens.Count < _numCameras)
            {
                _cameraScreens.Add(new CameraScreen());
            }
            if (_cameraScreens.Count > _numCameras)
            {
                _cameraScreens.RemoveRange(_numCameras - 1, _cameraScreens.Count - _numCameras);
            }

            for (int i = 0; i < _cameraScreens.Count; i++)
            {
                _cameraScreens[i].camera = (Camera)EditorGUILayout.ObjectField("Camera " + (i + 1), _cameraScreens[i].camera, typeof(Camera), true);
                _cameraScreens[i].screen = (GameObject)EditorGUILayout.ObjectField("Screen " + (i + 1), _cameraScreens[i].screen, typeof(GameObject), true);
            }

            if (GUI.changed)
            {
                if ((_numCameras != numCamerasBefore) || (_numEmptySides != numEmptySidesBefore))
                {
                    // Recompute _rotationY only when a manually set value might no longer make sense.
                    _rotationY = rotationYCentered();
                }
            }

            if (GUILayout.Button("Update"))
            {
                UpdateCameras();
            }

            EditorGUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            Save();
        }

        private float rotationYCentered()
        {
            // Rotates the screen so the positive X axis points to:
            // the middle of the middle screen for an odd number of screens
            // the middle edge between screens for an even number of screens
            int numSides = _numCameras + _numEmptySides;
            float fovDeg = 360.0f / numSides;
            return -(_numCameras - 1) * fovDeg / 2.0f + 90.0f;
        }

        private void UpdateCameras()
        {
            // TODO: Make the actions here undoable
            // https://docs.unity3d.com/ScriptReference/Undo.html

            if (_cameraScreens.Count != _numCameras)
            {
                Debug.Log("Exactly " + _numCameras + " camera/screen pairs are expected.");
                return;
            }

            // Create the objects if they are not specified.

            if (_mouse == null)
            {
                _mouse = new GameObject("Mouse");
                _mouse.transform.localPosition = new Vector3(0, 0, 0);

                // For some reason, creating objects in this routine does not seem to
                // mark the containing scene as dirty, so it is difficult to save the
                // scene.  As a work-around, manually force the dirty marking.
                SetObjectDirty(_mouse);

                if ((_mouseComponentExtra != null) && (_mouseComponentExtra.Length > 0))
                {
                    string fullName = _mouseComponentExtra + ",Assembly-CSharp";
                    Type t = Type.GetType(fullName);
                    if (t != null)
                    {
                        _mouse.AddComponent(t);
                    }
                    else
                    {
                        Debug.Log("Cannot find extra script of type '" + fullName + "'");
                    }
                }
            }

            for (int i = 0; i < _cameraScreens.Count; i++)
            {
                if (_cameraScreens[i].camera == null)
                {
                    GameObject cameraObj = new GameObject("MouseCamera" + (i + 1));
                    SetObjectDirty(cameraObj);
                    _cameraScreens[i].camera = cameraObj.AddComponent(typeof(Camera)) as Camera;

                    _cameraScreens[i].camera.targetDisplay = i + 1;
                }
                _cameraScreens[i].camera.transform.localRotation = Quaternion.identity;
                _cameraScreens[i].camera.transform.localPosition = Vector3.zero;
                if (_cameraScreens[i].screen == null)
                {
                    _cameraScreens[i].screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    SetObjectDirty(_cameraScreens[i].screen);
                    _cameraScreens[i].screen.name = "MouseCamera" + (i + 1) + "Screen";
                }
                _cameraScreens[i].screen.transform.localRotation = Quaternion.identity;
                _cameraScreens[i].screen.transform.localPosition = Vector3.zero;

                MeshRenderer renderer = _cameraScreens[i].screen.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                OffAxisPerspectiveCamera offAxisPerspectiveCamera =
                    _cameraScreens[i].camera.gameObject.AddComponent<OffAxisPerspectiveCamera>() as OffAxisPerspectiveCamera;
                offAxisPerspectiveCamera.screen = _cameraScreens[i].screen;
            }

            // If displays are rotated 90 degrees, so the image is taller than it is wide,
            // the Windows Display settings should indicate that the displays are in "Portrait" mode
            // (or "Portrait, flipped"). Then the text displayed by Windows will have the correct
            // orientation. Unity's Screen.resolutions is aware of this Windows Display setting.
            // So when the editor is placed on a screen that has been rotated and set to "Portrait",
            // then Screen.resolutions returns values with height greater than width.

            int numSides = _numCameras + _numEmptySides;
            float fovDeg = 360.0f / numSides;
            float fovRad = Mathf.PI * 2.0f / numSides;

            float camYRot = _rotationY;

            float viewDirTrans = (_screenWidth / 2.0f) / Mathf.Tan(fovRad / 2.0f);
            float heightDirTrans = _screenHeight / 2.0f - _fractionalHeight * _screenHeight;

            for (int i = 0; i < _cameraScreens.Count; i++)
            {
                Transform cameraXform = _cameraScreens[i].camera.gameObject.transform;
                cameraXform.SetParent(_mouse.transform);

                Transform screenXform = _cameraScreens[i].screen.transform;
                screenXform.SetParent(cameraXform);

                _cameraScreens[i].camera.nearClipPlane = _near;
                _cameraScreens[i].camera.farClipPlane = _far;

                cameraXform.localPosition = new Vector3(0, 0, 0);
                cameraXform.Rotate(0, camYRot, 0);

                screenXform.localPosition = new Vector3(0, heightDirTrans, viewDirTrans);
                screenXform.localScale = new Vector3(_screenWidth, _screenHeight, 1);

                // One way of making the screens invisible to their cameras.
                screenXform.Rotate(0, 180, 0);

                screenXform.position += new Vector3(_offsetX, 0, _offsetZ);

                camYRot += fovDeg;
            }
        }

        // Storing the state across sessions, using resources.

        private CamerasNGonSaved _saved;

        private void Save()
        {
            _saved.mouseName = PathName(_mouse);
            _saved.cameraNames.Clear();
            _saved.screenNames.Clear();
            foreach (CameraScreen cameraScreen in _cameraScreens)
            {
                GameObject cameraObj = (cameraScreen.camera != null) ? cameraScreen.camera.gameObject : null;
                _saved.cameraNames.Add(PathName(cameraObj));
                _saved.screenNames.Add(PathName(cameraScreen.screen));
            }
            _saved.numEmptySides = _numEmptySides;
            _saved.screenWidth = _screenWidth;
            _saved.screenHeight = _screenHeight;
            _saved.fractionalHeight = _fractionalHeight;
            _saved.rotationY = _rotationY;
            _saved.offsetX = _offsetX;
            _saved.offsetZ = _offsetZ;
            _saved.near = _near;
            _saved.far = _far;

            AssetDatabase.Refresh();
            EditorUtility.SetDirty(_saved);
            AssetDatabase.SaveAssets();
        }

        private void Load()
        {
            _saved = Resources.Load<CamerasNGonSaved>("Editor/savedCamerasNGon");
            if (_saved != null)
            {
                _mouse = GameObject.Find(_saved.mouseName);
                if (_saved.cameraNames.Count == _saved.screenNames.Count)
                {
                    _cameraScreens.Clear();
                    for (int i = 0; i < _saved.cameraNames.Count; i++)
                    {
                        CameraScreen cameraScreen = new CameraScreen();
                        GameObject cameraObj = GameObject.Find(_saved.cameraNames[i]);
                        cameraScreen.camera = (cameraObj != null) ? cameraObj.GetComponent<Camera>() : null;
                        cameraScreen.screen = GameObject.Find(_saved.screenNames[i]);
                        _cameraScreens.Add(cameraScreen);
                    }
                    _numCameras = _cameraScreens.Count;
                    _numEmptySides = _saved.numEmptySides;
                    _screenWidth = _saved.screenWidth;
                    _screenHeight = _saved.screenHeight;
                    _fractionalHeight = _saved.fractionalHeight;
                    _rotationY = _saved.rotationY;
                    _offsetX = _saved.offsetX;
                    _offsetZ = _saved.offsetZ;
                    _near = _saved.near;
                    _far = _saved.far;
                }
            }
            else
            {
                _saved = CreateInstance<CamerasNGonSaved>();

                string root = Application.dataPath;
                EnsureDirectory(root + "/Resources");
                EnsureDirectory(root + "/Resources/Editor");

                // Saving and loading work only if the filename has the extension ".asset".

                AssetDatabase.CreateAsset(_saved, "Assets/Resources/Editor/savedCamerasNGon.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception e)
                {
                    Debug.Log("Cannot create " + path + ": " + e.ToString());
                }
            }
        }

        private string PathName(GameObject o)
        {
            if (o == null)
            {
                return "";
            }
            string path = o.name;
            while (o.transform.parent != null)
            {
                o = o.transform.parent.gameObject;
                path = o.name + "/" + path;
            }
            return path;
        }

        private void SetObjectDirty(GameObject obj)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(obj);
                EditorSceneManager.MarkSceneDirty(obj.scene);
            }
        }
    }
}
