﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TgcViewer.Utils.TgcSceneLoader;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX;
using TgcViewer.Utils.TgcGeometry;
using TgcViewer.Utils.Input;
using TgcViewer;
using System.Drawing;

namespace Examples.MeshCreator.EditablePolyTools
{
    /// <summary>
    /// Herramienta para poder editar todos los componentes de un mesh: vertices, aristas y poligonos.
    /// </summary>
    public class EditablePoly
    {
        /// <summary>
        /// Epsilon para comparar si dos vertices son iguales
        /// </summary>
        private const float EPSILON = 0.0001f;

        /// <summary>
        /// Estado dentro del modo EditablePoly
        /// </summary>
        public enum State
        {
            SelectObject,
            SelectingObject,
            TranslateGizmo,
        }

        /// <summary>
        /// Tipo de primitiva de EditablePoly
        /// </summary>
        public enum PrimitiveType
        {
            Vertex,
            Edge,
            Polygon,
            None
        }

        TgcMesh mesh;
        List<Vertex> vertices;
        List<Edge> edges;
        List<Polygon> polygons;
        short[] indexBuffer;
        bool dirtyValues;
        PrimitiveType currentPrimitive;
        State currentState;
        bool selectiveObjectsAdditive;
        SelectionRectangleMesh rectMesh;
        Vector2 initMousePos;
        List<Primitive> selectionList;
        TgcPickingRay pickingRay;

        /// <summary>
        /// Construir un EditablePoly a partir de un mesh
        /// </summary>
        public EditablePoly(TgcMesh origMesh)
        {
            this.currentPrimitive = PrimitiveType.None;
            this.rectMesh = new SelectionRectangleMesh();
            this.selectionList = new List<Primitive>();
            this.pickingRay = new TgcPickingRay();
            loadMesh(origMesh);
        }

        public void setPrimitiveType(PrimitiveType p)
        {
            //TODO deseleccionar si habia algo antes

            this.currentPrimitive = p;
        }

        /// <summary>
        /// Actualizacion de estado en render loop
        /// </summary>
        public void update()
        {
            //Maquina de estados
            switch (currentState)
            {
                case State.SelectObject:
                    doSelectObject();
                    break;
                case State.SelectingObject:
                    doSelectingObject();
                    break;
                case State.TranslateGizmo:
                    doTranslateGizmo();
                    break;
            }
        }



        #region Primitive selection
       

        /// <summary>
        /// Estado: seleccionar objetos (estado default)
        /// </summary>
        private void doSelectObject()
        {
            TgcD3dInput input = GuiController.Instance.D3dInput;

            //Si mantiene control y clic con el mouse, iniciar cuadro de seleccion para agregar/quitar a la seleccion actual
            if ((input.keyDown(Microsoft.DirectX.DirectInput.Key.LeftControl) || input.keyDown(Microsoft.DirectX.DirectInput.Key.RightControl))
                && input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                currentState = State.SelectingObject;
                this.initMousePos = new Vector2(input.Xpos, input.Ypos);
                this.selectiveObjectsAdditive = true;
            }
            //Si mantiene el clic con el mouse, iniciar cuadro de seleccion
            else if (input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                currentState = State.SelectingObject;
                this.initMousePos = new Vector2(input.Xpos, input.Ypos);
                this.selectiveObjectsAdditive = false;
            }
        }

        /// <summary>
        /// Estado: Cuando se esta arrastrando el mouse para armar el cuadro de seleccion
        /// </summary>
        private void doSelectingObject()
        {
            TgcD3dInput input = GuiController.Instance.D3dInput;

            //Mantiene el mouse apretado
            if (input.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                //Definir recuadro
                Vector2 mousePos = new Vector2(input.Xpos, input.Ypos);
                Vector2 min = Vector2.Minimize(initMousePos, mousePos);
                Vector2 max = Vector2.Maximize(initMousePos, mousePos);

                rectMesh.updateMesh(min, max);

            }
            //Solo el mouse
            else if (input.buttonUp(TgcD3dInput.MouseButtons.BUTTON_LEFT))
            {
                //Definir recuadro
                Vector2 mousePos = new Vector2(input.Xpos, input.Ypos);
                Vector2 min = Vector2.Minimize(initMousePos, mousePos);
                Vector2 max = Vector2.Maximize(initMousePos, mousePos);
                Rectangle r = new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));

                //Usar recuadro de seleccion solo si tiene un tamaño minimo
                if (r.Width > 1 && r.Height > 1)
                {
                    //Limpiar seleccionar anterior si no estamos agregando en forma aditiva
                    if (!selectiveObjectsAdditive)
                    {
                        clearSelection();
                    }

                    //Buscar que primitivas caen dentro de la seleccion y elegirlos
                    int i = 0;
                    Primitive p = iteratePrimitive(currentPrimitive, i);
                    while (p != null)
                    {
                        //Ver si hay colision contra la proyeccion de la primitiva y el rectangulo 2D
                        Rectangle primRect;
                        if (p.projectToScreen(out primRect))
                        {
                            if (r.IntersectsWith(primRect))
                            {
                                //Agregar el objeto en forma aditiva
                                if (selectiveObjectsAdditive)
                                {
                                    selectOrRemovePrimitiveIfPresent(p);
                                }
                                //Agregar el objeto en forma simple
                                else
                                {
                                    selectPrimitive(p);
                                }
                            }
                        }
                        p = iteratePrimitive(currentPrimitive, ++i);
                    }
                }
                //Si el recuadro no tiene tamaño suficiente, hacer seleccion directa
                else
                {
                    doDirectSelection(selectiveObjectsAdditive);
                }

                currentState = State.SelectObject;

                //Si quedo algo seleccionado activar gizmo
                if (selectionList.Count > 0)
                {
                    activateTranslateGizmo();
                }

                //Actualizar panel de Modify con lo que se haya seleccionado, o lo que no
                //control.updateModifyPanel();
            }



            //Dibujar recuadro
            rectMesh.render();
        }

        

        /// <summary>
        /// Seleccionar una sola primitiva
        /// </summary>
        private void selectPrimitive(Primitive p)
        {
            selectionList.Add(p);
        }

        /// <summary>
        /// Selecciona una sola primitiva pero antes se fija si ya no estaba en la lista de seleccion.
        /// Si ya estaba, entonces la quita de la lista de seleccion
        /// </summary>
        private void selectOrRemovePrimitiveIfPresent(Primitive p)
        {
            //Ya existe, quitar
            if (selectionList.Contains(p))
            {
                selectionList.Remove(p);
            }
            //No existe, agregar
            else
            {
                selectionList.Add(p);
            }
        }

        /// <summary>
        /// Deseleccionar todo
        /// </summary>
        private void clearSelection()
        {
            selectionList.Clear();
        }

        /// <summary>
        /// Hacer picking para seleccionar la primitiva mas cercano del ecenario.
        /// </summary>
        /// <param name="additive">En True agrega/quita la primitiva a la seleccion actual</param>
        private void doDirectSelection(bool additive)
        {
            this.pickingRay.updateRay();

            //Buscar menor colision con primitivas
            float minDistSq = float.MaxValue;
            Primitive closestPrimitive = null;
            Vector3 q;
            int i = 0;
            Primitive p = iteratePrimitive(currentPrimitive, i);
            while (p != null)
            {
                if (p.intersectRay(pickingRay.Ray, out q))
                {
                    float lengthSq = Vector3.Subtract(pickingRay.Ray.Origin, q).LengthSq();
                    if (lengthSq < minDistSq)
                    {
                        minDistSq = lengthSq;
                        closestPrimitive = p;
                    }
                }
                p = iteratePrimitive(currentPrimitive, ++i);
            }

            //Agregar
            if (closestPrimitive != null)
            {
                //Sumar a la lista de seleccion
                if (additive)
                {
                    selectOrRemovePrimitiveIfPresent(closestPrimitive);
                }
                //Seleccionar uno solo
                else
                {
                    clearSelection();
                    selectPrimitive(closestPrimitive);
                }
                activateTranslateGizmo();
            }
            //Nada seleccionado
            else
            {
                //Limpiar seleccion
                clearSelection();
            }

            //Pasar a modo seleccion
            currentState = State.SelectObject;
            //control.updateModifyPanel();
        }


        #endregion



        private void activateTranslateGizmo()
        {
            //TODO gizmo translate
        }

        private void doTranslateGizmo()
        {
            //TODO gizmo translate
        }

        public void render()
        {
            //Actualizar mesh si hubo algun cambio
            if (dirtyValues)
            {
                updateMesh();
                dirtyValues = false;
            }

            //Render de mesh
            mesh.render();

            //Render de primitivas seleccionadas
            foreach (Primitive p in selectionList)
            {
                p.render(true, mesh.Transform);
            }
        }

        public void dispose()
        {
        }

        





        #region Mesh loading

        /// <summary>
        /// Tomar un mesh cargar todas las estructuras internas necesarias para poder editarlo
        /// </summary>
        private void loadMesh(TgcMesh origMesh)
        {
            //Obtener vertices del mesh
            this.mesh = origMesh;
            List<Vertex> origVertices = getMeshOriginalVertexData(origMesh);
            int origTriCount = origVertices.Count / 3;

            //Iterar sobre los triangulos y generar data auxiliar unificada
            vertices = new List<Vertex>();
            edges = new List<Edge>();
            polygons = new List<Polygon>();
            indexBuffer = new short[origTriCount * 3];
            int[] attributeBuffer = origMesh.D3dMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
            origMesh.D3dMesh.UnlockAttributeBuffer(attributeBuffer);
            for (int i = 0; i < origTriCount; i++)
            {
                Vertex v1 = origVertices[i * 3];
                Vertex v2 = origVertices[i * 3 + 1];
                Vertex v3 = origVertices[i * 3 + 2];

                //Agregar vertices a la lista, si es que son nuevos
                int v1Idx = addVertexToListIfUnique(v1);
                int v2Idx = addVertexToListIfUnique(v2);
                int v3Idx = addVertexToListIfUnique(v3);
                v1 = vertices[v1Idx];
                v2 = vertices[v2Idx];
                v3 = vertices[v3Idx];

                //TODO: agregar vertices al vertexBuffer

                //Crear edges (vertices ordenados segun indice ascendente)
                Edge e1 = new Edge();
                e1.a = vertices[FastMath.Min(v1Idx, v2Idx)];
                e1.b = vertices[FastMath.Max(v1Idx, v2Idx)];
                Edge e2 = new Edge();
                e2.a = vertices[FastMath.Min(v2Idx, v3Idx)];
                e2.b = vertices[FastMath.Max(v2Idx, v3Idx)];
                Edge e3 = new Edge();
                e3.a = vertices[FastMath.Min(v3Idx, v1Idx)];
                e3.b = vertices[FastMath.Max(v3Idx, v1Idx)];

                //Agregar edges a la lista, si es que son nuevos
                int e1Idx = addEdgeToListIfUnique(e1);
                int e2Idx = addEdgeToListIfUnique(e2);
                int e3Idx = addEdgeToListIfUnique(e3);
                e1 = edges[e1Idx];
                e2 = edges[e2Idx];
                e3 = edges[e3Idx];

                //Guardar referencias a aristas en cada vertice
                v1.edges.Add(e1);
                v1.edges.Add(e3);
                v2.edges.Add(e1);
                v2.edges.Add(e2);
                v3.edges.Add(e2);
                v3.edges.Add(e3);

                //Crear poligono para este triangulo
                Polygon p = new Polygon();
                p.vertices = new List<Vertex>();
                p.vertices.Add(v1);
                p.vertices.Add(v2);
                p.vertices.Add(v3);
                p.edges = new List<Edge>();
                p.edges.Add(e1);
                p.edges.Add(e2);
                p.edges.Add(e3);
                p.vbTriangles = new List<int>();
                p.vbTriangles.Add(i * 3);
                p.plane = Plane.FromPoints(v1.position, v2.position, v3.position);
                p.plane.Normalize();
                p.matId = attributeBuffer[i];

                //Agregar triangulo al index buffer
                indexBuffer[i * 3] = (short)v1Idx;
                indexBuffer[i * 3 + 1] = (short)v2Idx;
                indexBuffer[i * 3 + 2] = (short)v3Idx;

                //Buscar si hay un poligono ya existente al cual sumarnos (coplanar y que compartan una arista)
                Polygon coplanarP = null;
                for (int j = 0; j < polygons.Count; j++)
                {
                    //Coplanares y con igual material ID
                    Polygon p0 = polygons[j];
                    if (p0.matId == p.matId && samePlane(p0.plane, p.plane))
                    {
                        //Buscar si tienen una arista igual
                        int p0SharedEdgeIdx;
                        int pSharedEdgeIdx;
                        if (findShareEdgeBetweenPolygons(p0, p, out p0SharedEdgeIdx, out pSharedEdgeIdx))
                        {
                            //Obtener el tercer vertice del triangulo que no es parte de la arista compartida
                            Edge sharedEdge = p.edges[pSharedEdgeIdx];
                            Vertex thirdVert;
                            if (p.vertices[0] != sharedEdge.a && p.vertices[0] != sharedEdge.b)
                                thirdVert = p.vertices[0];
                            else if (p.vertices[1] != sharedEdge.a && p.vertices[1] != sharedEdge.b)
                                thirdVert = p.vertices[1];
                            else
                                thirdVert = p.vertices[2];

                            //Quitar arista compartida de poligono existente
                            p0.edges.RemoveAt(p0SharedEdgeIdx);

                            //Agregar el tercer vertice a poligno existente
                            p0.vertices.Add(thirdVert);

                            //Eliminar arista compartida de la lista global
                            for (int k = 0; k < edges.Count; k++)
                            {
                                if (sameEdge(edges[k], sharedEdge))
                                {
                                    edges.RemoveAt(k);
                                    break;
                                }
                            }

                            //Agregar al poligono dos nuevas aristas que conectar los extremos de la arista compartida hacia el tercer vertice
                            Edge newPolEdge1 = new Edge();
                            newPolEdge1.a = vertices[FastMath.Min(sharedEdge.a.vbIndex, thirdVert.vbIndex)];
                            newPolEdge1.b = vertices[FastMath.Max(sharedEdge.a.vbIndex, thirdVert.vbIndex)];
                            newPolEdge1.faces = new List<Polygon>();
                            newPolEdge1.faces.Add(p0);
                            sharedEdge.a.edges.Add(newPolEdge1);
                            sharedEdge.b.edges.Add(newPolEdge1);
                            p0.edges.Add(newPolEdge1);

                            Edge newPolEdge2 = new Edge();
                            newPolEdge2.a = vertices[FastMath.Min(sharedEdge.b.vbIndex, thirdVert.vbIndex)];
                            newPolEdge2.b = vertices[FastMath.Max(sharedEdge.b.vbIndex, thirdVert.vbIndex)];
                            newPolEdge2.faces = new List<Polygon>();
                            newPolEdge2.faces.Add(p0);
                            sharedEdge.a.edges.Add(newPolEdge2);
                            sharedEdge.b.edges.Add(newPolEdge2);
                            p0.edges.Add(newPolEdge2);

                            //Agregar indice de triangulo del vertexBuffer que se sumo al poligono
                            p0.vbTriangles.Add(p.vbTriangles[0]);

                            coplanarP = p0;
                        }
                    }
                }
                //Es un nuevo poligono, agregarlo
                if (coplanarP == null)
                {
                    polygons.Add(p);
                }
            }

            dirtyValues = true;
        }


        /// <summary>
        /// Busca si ambos poligonos tienen una arista igual.
        /// Si encontro retorna el indice de la arista igual de cada poligono.
        /// </summary>
        private bool findShareEdgeBetweenPolygons(Polygon p1, Polygon p2, out int p1Edge, out int p2Edge)
        {
            for (int i = 0; i < p1.edges.Count; i++)
            {
                for (int j = 0; j < p2.edges.Count; j++)
                {
                    if (sameEdge(p1.edges[i], p2.edges[j]))
                    {
                        p1Edge = i;
                        p2Edge = j;
                        return true;
                    }
                }
            }
            p1Edge = -1;
            p2Edge = -1;
            return false;
        }

        /// <summary>
        /// Agrega una nueva arista a la lista si es que ya no hay otra igual.
        /// Devuelve el indice de la nuevo arista o de la que ya estaba.
        /// </summary>
        private int addEdgeToListIfUnique(Edge e)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                if (sameEdge(edges[i], e))
                {
                    return i;
                }
            }
            e.faces = new List<Polygon>();
            edges.Add(e);
            return edges.Count - 1;
        }


        /// <summary>
        /// Agrega un nuevo vertice a la lista si es que ya no hay otro igual.
        /// Devuelve el indice del nuevo vertice o del que ya estaba.
        /// </summary>
        private int addVertexToListIfUnique(Vertex v)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (sameVextex(vertices[i], v))
                {
                    return i;
                }
            }
            v.vbIndex = vertices.Count;
            v.edges = new List<Edge>();
            vertices.Add(v);
            return v.vbIndex;
        }

        /// <summary>
        /// Indica si dos aristas son iguales
        /// </summary>
        private bool sameEdge(Edge e1, Edge e2)
        {
            return sameVextex(e1.a, e2.a) && sameVextex(e1.b, e2.b);
        }

        /// <summary>
        /// Indica si dos vertices son iguales
        /// </summary>
        /// <returns></returns>
        private bool sameVextex(Vertex a, Vertex b)
        {
            return equalsVector3(a.position, b.position);
        }

        /// <summary>
        /// Indica si dos Vector3 son iguales
        /// </summary>
        private bool equalsVector3(Vector3 a, Vector3 b)
        {
            return equalsFloat(a.X, b.X)
                && equalsFloat(a.Y, b.Y)
                && equalsFloat(a.Z, b.Z);
        }

        /// <summary>
        /// Compara que dos floats sean iguales, o casi
        /// </summary>
        private bool equalsFloat(float f1, float f2)
        {
            return FastMath.Abs(f1 - f2) <= EPSILON;
        }

        /// <summary>
        /// Compara si dos planos son iguales
        /// </summary>
        private bool samePlane(Plane p1, Plane p2)
        {
            //TODO: comparar en ambos sentidos por las dudas
            return equalsVector3(new Vector3(p1.A, p1.B, p1.C), new Vector3(p2.A, p2.B, p2.C))
                && equalsFloat(p1.D, p2.D);
        }

        /// <summary>
        /// Obtener la lista de vertices originales del mesh
        /// </summary>
        private List<Vertex> getMeshOriginalVertexData(TgcMesh origMesh)
        {
            List<Vertex> origVertices = new List<Vertex>();
            switch (origMesh.RenderType)
            {
                case TgcMesh.MeshRenderType.VERTEX_COLOR:
                    TgcSceneLoader.VertexColorVertex[] verts1 = (TgcSceneLoader.VertexColorVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.VertexColorVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts1.Length; i++)
                    {
                        Vertex v = new Vertex();
                        v.position = verts1[i].Position;
                        /*v.normal = verts1[i].Normal;
                        v.color = verts1[i].Color;*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;

                case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                    TgcSceneLoader.DiffuseMapVertex[] verts2 = (TgcSceneLoader.DiffuseMapVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts2.Length; i++)
                    {
                        Vertex v = new Vertex();
                        v.position = verts2[i].Position;
                        /*v.normal = verts2[i].Normal;
                        v.texCoords = new Vector2(verts2[i].Tu, verts2[i].Tv);
                        v.color = verts2[i].Color;*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;

                case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                    TgcSceneLoader.DiffuseMapAndLightmapVertex[] verts3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])origMesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.ReadOnly, origMesh.D3dMesh.NumberVertices);
                    for (int i = 0; i < verts3.Length; i++)
                    {
                        Vertex v = new Vertex();
                        v.position = verts3[i].Position;
                        /*v.normal = verts3[i].Normal;
                        v.texCoords = new Vector2(verts3[i].Tu0, verts3[i].Tv0);
                        v.color = verts3[i].Color;
                        v.texCoords2 = new Vector2(verts3[i].Tu1, verts3[i].Tv1);*/
                        origVertices.Add(v);
                    }
                    origMesh.D3dMesh.UnlockVertexBuffer();
                    break;
            }

            return origVertices;
        }

        /// <summary>
        /// Actualizar vertexBuffer de mesh original en base a la estructura interna del editablePoly
        /// </summary>
        private void updateMesh()
        {
            //Actualizar vertexBuffer
            using (VertexBuffer vb = mesh.D3dMesh.VertexBuffer)
            {
                switch (mesh.RenderType)
                {
                    case TgcMesh.MeshRenderType.VERTEX_COLOR:
                        TgcSceneLoader.VertexColorVertex[] verts1 = (TgcSceneLoader.VertexColorVertex[])mesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.VertexColorVertex), LockFlags.None, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts1.Length; i++)
                        {
                            verts1[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts1, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP:
                        TgcSceneLoader.DiffuseMapVertex[] verts2 = (TgcSceneLoader.DiffuseMapVertex[])mesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts2.Length; i++)
                        {
                            verts2[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts2, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                    case TgcMesh.MeshRenderType.DIFFUSE_MAP_AND_LIGHTMAP:
                        TgcSceneLoader.DiffuseMapAndLightmapVertex[] verts3 = (TgcSceneLoader.DiffuseMapAndLightmapVertex[])mesh.D3dMesh.LockVertexBuffer(
                        typeof(TgcSceneLoader.DiffuseMapAndLightmapVertex), LockFlags.ReadOnly, mesh.D3dMesh.NumberVertices);
                        for (int i = 0; i < verts3.Length; i++)
                        {
                            verts3[i].Position = vertices[indexBuffer[i]].position;
                        }
                        mesh.D3dMesh.SetVertexBufferData(verts3, LockFlags.None);
                        mesh.D3dMesh.UnlockVertexBuffer();
                        break;
                }

            }

            //Actualizar indexBuffer (en forma secuencial)
            using (IndexBuffer ib = mesh.D3dMesh.IndexBuffer)
            {
                short[] seqIndexBuffer = new short[indexBuffer.Length];
                for (int i = 0; i < seqIndexBuffer.Length; i++)
                {
                    seqIndexBuffer[i] = (short)i;
                }
                ib.SetData(seqIndexBuffer, 0, LockFlags.None);
            }

            //Actualizar attributeBuffer
            int[] attributeBuffer = mesh.D3dMesh.LockAttributeBufferArray(LockFlags.None);
            foreach (Polygon p in polygons)
            {
                //Setear en cada triangulo el material ID del poligono
                foreach (int idx in p.vbTriangles)
                {
                    int triIdx = idx / 3;
                    attributeBuffer[triIdx] = p.matId;
                }
            }
            mesh.D3dMesh.UnlockAttributeBuffer(attributeBuffer);
        }

        /// <summary>
        /// Actualizar estructuras internas en base a mesh original
        /// </summary>
        public void updateValuesFromMesh(TgcMesh mesh)
        {
            this.mesh = mesh;
            List<Vertex> origVertices = getMeshOriginalVertexData(mesh);
            for (int i = 0; i < origVertices.Count; i++)
            {
                Vertex origV = origVertices[i];
                Vertex v = vertices[indexBuffer[i]];
                v.position = origV.position;
                /*v.normal = origV.normal;
                v.color = origV.color;
                v.texCoords = origV.texCoords;
                v.texCoords2 = origV.texCoords2;*/
            }
            dirtyValues = true;
        }


        #endregion



        #region Estructuras auxiliares

        /// <summary>
        /// Primitiva generica
        /// </summary>
        private abstract class Primitive
        {
            /// <summary>
            /// Tipo de primitiva
            /// </summary>
            public abstract PrimitiveType Type {get;}

            /// <summary>
            /// Proyectar primitva a rectangulo 2D en la pantalla
            /// </summary>
            /// <param name="box2D">Rectangulo 2D proyectado</param>
            /// <returns>False si es un caso degenerado de proyeccion y no debe considerarse</returns>
            public abstract bool projectToScreen(out Rectangle box2D);

            /// <summary>
            /// Intersect ray againts primitive
            /// </summary>
            public abstract bool intersectRay(TgcRay tgcRay, out Vector3 q);

            /// <summary>
            /// Dibujar primitiva
            /// </summary>
            public abstract void render(bool selected, Matrix meshTransform);
        }

        /// <summary>
        /// Estructura auxiliar de vertice
        /// </summary>
        private class Vertex : Primitive
        {
            /// <summary>
            /// Sphere for ray-collisions
            /// </summary>
            private static readonly TgcBoundingSphere COMMON_SPHERE = new TgcBoundingSphere(new Vector3(0, 0, 0), 2);

            public Vector3 position;
            /*public Vector3 normal;
            public Vector2 texCoords;
            public Vector2 texCoords2;
            public int color;*/
            public List<Edge> edges;
            public int vbIndex;

            public override string ToString()
            {
                return "Index: " + vbIndex + ", Pos: " + TgcParserUtils.printVector3(position);
            }

            public override PrimitiveType Type
            {
                get { return PrimitiveType.Vertex; }
            }

            public override bool projectToScreen(out Rectangle box2D)
            {
                return MeshCreatorUtils.projectPoint(position, out box2D);
            }

            public override bool intersectRay(TgcRay ray, out Vector3 q)
            {
                COMMON_SPHERE.setCenter(this.position);
                float t;
                return TgcCollisionUtils.intersectRaySphere(ray, COMMON_SPHERE, out t, out q);
            }

            public override void render(bool selected, Matrix meshTransform)
            {
                COMMON_SPHERE.setCenter(Vector3.TransformCoordinate(this.position, meshTransform));
                COMMON_SPHERE.render();
            }
        }

        /// <summary>
        /// Estructura auxiliar de arista
        /// </summary>
        private class Edge : Primitive
        {
            public Vertex a;
            public Vertex b;
            public List<Polygon> faces;

            public override string ToString()
            {
                return a.vbIndex + " => " + b.vbIndex;
            }

            public override PrimitiveType Type
            {
                get { return PrimitiveType.Edge; }
            }

            public override bool projectToScreen(out Rectangle box2D)
            {
                return MeshCreatorUtils.projectSegmentToScreenRect(a.position, b.position, out box2D);
            }

            public override bool intersectRay(TgcRay ray, out Vector3 q)
            {
                //TODO: hacer ray-obb (hacer un obb previamente para el edge)
                throw new NotImplementedException();
            }

            public override void render(bool selected, Matrix meshTransform)
            {
                //TODO: dibujar linea
            }
        }

        /// <summary>
        /// Estructura auxiliar de poligono
        /// </summary>
        private class Polygon : Primitive
        {
            public List<Vertex> vertices;
            public List<Edge> edges;
            public List<int> vbTriangles;
            public Plane plane;
            public int matId;

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < vertices.Count; i++)
                {
                    sb.Append(vertices[i].vbIndex + ", ");
                }
                sb.Remove(sb.Length - 2, 2);
                return sb.ToString();
            }

            public override PrimitiveType Type
            {
                get { return PrimitiveType.Polygon; }
            }

            public override bool projectToScreen(out Rectangle box2D)
            {
                Vector3[] v = new Vector3[vertices.Count];
                for (int i = 0; i < v.Length; i++)
			    {
                    v[i] = vertices[i].position;
			    }
                return MeshCreatorUtils.projectPolygon(v, out box2D);
            }

            public override bool intersectRay(TgcRay ray, out Vector3 q)
            {
                //TODO: implementar colision ray-polygon (primero ray-plane y luego point-polygon)
                throw new NotImplementedException();
            }

            public override void render(bool selected, Matrix meshTransform)
            {
                //TODO: dibujar poligono
            }
        }

        /// <summary>
        /// Iterar sobre lista de primitivas
        /// </summary>
        /// <param name="primitiveType">Primitiva</param>
        /// <param name="i">indice</param>
        /// <returns>Elemento o null si no hay mas</returns>
        private Primitive iteratePrimitive(PrimitiveType primitiveType, int i)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Vertex:
                    if (i == vertices.Count) return null;
                    return vertices[i];
                case PrimitiveType.Edge:
                    if (i == edges.Count) return null;
                    return edges[i];
                case PrimitiveType.Polygon:
                    if (i == polygons.Count) return null;
                    return polygons[i];
            }
            return null;
        }

        #endregion

        




        
    }
}