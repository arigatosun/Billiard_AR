using System.Collections.Generic;
using UnityEngine;

namespace ibc.unity
{
    using ibc.objects;
    using utility;

    /// <summary>Class UnityPolygon provides points that define a polygon in the unity scene.
    /// Implements the <see cref="MonoBehaviour" /></summary>
    [ExecuteInEditMode()]
    public class UnityPolygon : MonoBehaviour, IIdentifiable
    {
        public int Identifier;
        public Vector3[] Points;
        public bool DrawMesh;

#if UNITY_EDITOR
        private Mesh mesh;
        private Material mat;
        private void OnEnable()
        {
            if (mesh != null) DestroyImmediate(mesh);
            if (mat != null) DestroyImmediate(mat);

            mesh = new Mesh();
            mat = new Material(Shader.Find("Standard"));
        }


        private void Update()
        {
            if (Points == null || Points.Length < 3 || !DrawMesh)
                return;

            mat.SetPass(0);

            var triangulator = new Triangulator(Points);
            var indices = triangulator.Triangulate();

            if (mesh == null)
                mesh = new Mesh();

            mesh.vertices = Points;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            Graphics.DrawMesh(mesh, Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one), mat, 0);
        }
#endif

        public int GetIdentifier()
        {
            return Identifier;
        }
    }
}