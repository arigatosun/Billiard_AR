using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace ibc.editors
{
    using unity;

    [CustomEditor(typeof(UnityCushion))]
    public class UnityCushionEditor : Editor
    {
        private UnityCushion _target;
        private bool _needsRepaint;
        private SelectionInfo _selectionInfo;


        void OnSceneGUI()
        {
            Event guiEvent = Event.current;

            if (guiEvent.type == EventType.Repaint)
            {
                Draw();
            }
            else if (guiEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }
            else
            {
                HandleInput(guiEvent);
                if (_needsRepaint)
                {
                    HandleUtility.Repaint();
                }
            }
        }

        void HandleInput(Event guiEvent)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(guiEvent.mousePosition);
            float drawPlaneHeight = 0;
            float dstToDrawPlane = (drawPlaneHeight - mouseRay.origin.y) / mouseRay.direction.y;
            Vector3 mousePosition = mouseRay.GetPoint(dstToDrawPlane);

            if (guiEvent.type == EventType.MouseDown && guiEvent.button == 0 && guiEvent.modifiers == EventModifiers.None)
            {
                HandleLeftMouseDown(mousePosition);
            }

            if (guiEvent.type == EventType.MouseUp && guiEvent.button == 0 && guiEvent.modifiers == EventModifiers.None)
            {
                HandleLeftMouseUp(mousePosition);
            }

            if (guiEvent.type == EventType.MouseDrag && guiEvent.button == 0 && guiEvent.modifiers == EventModifiers.None)
            {
                HandleLeftMouseDrag(mousePosition);
            }

            if (!_selectionInfo.pointIsSelected)
            {
                UpdateMouseOverInfo(mousePosition);
            }

        }

        void HandleLeftMouseDown(Vector3 mousePosition)
        {
            if (_selectionInfo.mouseIsOverPoint)
            {

                _selectionInfo.pointIsSelected = true;
                _selectionInfo.positionAtStartOfDrag = mousePosition;
                _needsRepaint = true;
            }
        }

        void HandleLeftMouseUp(Vector3 mousePosition)
        {
            if (_selectionInfo.pointIsSelected)
            {
                _target.Points[_selectionInfo.pointIndex] = _selectionInfo.positionAtStartOfDrag;
                Undo.RecordObject(_target, "Move point");
                _target.Points[_selectionInfo.pointIndex] = mousePosition;

                _selectionInfo.pointIsSelected = false;
                _selectionInfo.pointIndex = -1;
                _needsRepaint = true;
            }

        }

        void HandleLeftMouseDrag(Vector3 mousePosition)
        {
            if (_selectionInfo.pointIsSelected)
            {
                _target.Points[_selectionInfo.pointIndex] = mousePosition;
                _needsRepaint = true;
            }

        }

        void UpdateMouseOverInfo(Vector3 mousePosition)
        {
            int mouseOverPointIndex = -1;
            for (int i = 0; i < _target.Points.Count; i++)
            {
                if (Vector3.Distance(mousePosition, _target.Points[i]) < _target.handleRadius)
                {
                    mouseOverPointIndex = i;
                    break;
                }
            }

            if (mouseOverPointIndex != _selectionInfo.pointIndex)
            {
                _selectionInfo.pointIndex = mouseOverPointIndex;
                _selectionInfo.mouseIsOverPoint = mouseOverPointIndex != -1;

                _needsRepaint = true;
            }

            if (_selectionInfo.mouseIsOverPoint)
            {
                _selectionInfo.mouseIsOverLine = false;
                _selectionInfo.lineIndex = -1;
            }
            else
            {
                int mouseOverLineIndex = -1;
                float closestLineDst = _target.handleRadius;
                for (int i = 0; i < _target.Points.Count; i++)
                {
                    float3 nextPointInShape = _target.Points[(i + 1) % _target.Points.Count];
                    float dstFromMouseToLine = HandleUtility.DistancePointToLineSegment(((float3)mousePosition).xz, _target.Points[i].xz, nextPointInShape.xz);
                    if (dstFromMouseToLine < closestLineDst)
                    {
                        closestLineDst = dstFromMouseToLine;
                        mouseOverLineIndex = i;
                    }
                }

                if (_selectionInfo.lineIndex != mouseOverLineIndex)
                {
                    _selectionInfo.lineIndex = mouseOverLineIndex;
                    _selectionInfo.mouseIsOverLine = mouseOverLineIndex != -1;
                    _needsRepaint = true;
                }
            }
        }

        void OnDrawGizmos()
        {
            Draw();
        }

        void Draw()
        {
            float3 height = new float3(0, 1, 0) * _target.Height;
            for (int i = 0; i < _target.Points.Count; i++)
            {
                Vector3 nextPoint = _target.Points[(i + 1) % _target.Points.Count] + height;
                if (i == _selectionInfo.lineIndex)
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(_target.Points[i] + height, nextPoint,  4);
                }
                else
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(_target.Points[i] + height, nextPoint, 2);
                }

                if (i == _selectionInfo.pointIndex)
                {
                    Handles.color = (_selectionInfo.pointIsSelected) ? Color.yellow : Color.red;
                    Handles.DrawSolidDisc(_target.Points[i] + height, Vector3.up, _target.handleRadius);

                }
                else
                {
                    Handles.color = Color.red;
                    Handles.DrawSolidDisc(_target.Points[i] + height, Vector3.up, _target.handleRadius);
                }
            }
            _needsRepaint = false;
        }

        void OnEnable()
        {
            _target = target as UnityCushion;
            _selectionInfo = new SelectionInfo();
        }

        public class SelectionInfo
        {
            public int pointIndex = -1;
            public bool mouseIsOverPoint;
            public bool pointIsSelected;
            public float3 positionAtStartOfDrag;

            public int lineIndex = -1;
            public bool mouseIsOverLine;
        }

    }
}