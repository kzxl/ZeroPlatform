using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Scene;

namespace Zero3D.IO
{
    /// <summary>
    /// Pure C# glTF 2.0 and binary GLB 3D model loader supporting scene hierarchy, transforms, meshes, and accessors.
    /// Also provides unified multi-format automatic loader (.obj, .stl, .gltf, .glb).
    /// </summary>
    public static class GltfLoader
    {
        private const uint GlbMagic = 0x46546C67; // 'glTF'
        private const uint ChunkTypeJson = 0x4E4F534A; // 'JSON'
        private const uint ChunkTypeBin = 0x004E4942; // 'BIN\0'

        /// <summary>
        /// Automatically identifies and loads a 3D model (.obj, .stl, .gltf, .glb) from disk into a SceneNode.
        /// </summary>
        public static SceneNode LoadAuto(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Model file not found: {filePath}", filePath);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".obj")
            {
                return ObjLoader.LoadFromFile(filePath);
            }
            if (ext == ".stl")
            {
                var mesh = StlLoader.LoadFromFile(filePath);
                return new SceneNode { Name = Path.GetFileNameWithoutExtension(filePath), Mesh = mesh };
            }
            if (ext == ".glb")
            {
                return LoadGlbFromFile(filePath);
            }
            if (ext == ".gltf")
            {
                return LoadGltfFromFile(filePath);
            }

            // Fallback inspect magic bytes
            byte[] header = new byte[4];
            using (var fs = File.OpenRead(filePath))
            {
                fs.Read(header, 0, 4);
            }
            uint magic = BitConverter.ToUInt32(header, 0);
            if (magic == GlbMagic)
            {
                return LoadGlbFromFile(filePath);
            }

            // Default try OBJ text
            return ObjLoader.LoadFromFile(filePath);
        }

        /// <summary>
        /// Loads a binary GLB file from disk into a hierarchical <see cref="SceneNode"/>.
        /// </summary>
        public static SceneNode LoadGlbFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"GLB file not found: {filePath}", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            string name = Path.GetFileNameWithoutExtension(filePath);
            return LoadGlb(bytes, name);
        }

        /// <summary>
        /// Loads a binary GLB byte array into a hierarchical <see cref="SceneNode"/>.
        /// </summary>
        public static SceneNode LoadGlb(byte[] glbBytes, string modelName = "GlbModel")
        {
            if (glbBytes == null || glbBytes.Length < 12)
                throw new InvalidDataException("Invalid GLB buffer: data too small to contain header.");

            uint magic = BitConverter.ToUInt32(glbBytes, 0);
            if (magic != GlbMagic)
                throw new InvalidDataException($"Invalid GLB magic header: 0x{magic:X8} (expected 0x{GlbMagic:X8}).");

            uint version = BitConverter.ToUInt32(glbBytes, 4);
            if (version != 2)
                throw new NotSupportedException($"Only glTF version 2 is supported (got version {version}).");

            uint totalLength = BitConverter.ToUInt32(glbBytes, 8);
            int offset = 12;

            string? jsonText = null;
            byte[]? binBuffer = null;

            while (offset + 8 <= glbBytes.Length && offset < totalLength)
            {
                uint chunkLength = BitConverter.ToUInt32(glbBytes, offset);
                uint chunkType = BitConverter.ToUInt32(glbBytes, offset + 4);
                offset += 8;

                if (offset + chunkLength > glbBytes.Length)
                    break;

                if (chunkType == ChunkTypeJson && jsonText == null)
                {
                    jsonText = Encoding.UTF8.GetString(glbBytes, offset, (int)chunkLength);
                }
                else if (chunkType == ChunkTypeBin && binBuffer == null)
                {
                    binBuffer = new byte[chunkLength];
                    Buffer.BlockCopy(glbBytes, offset, binBuffer, 0, (int)chunkLength);
                }

                offset += (int)chunkLength;
            }

            if (string.IsNullOrEmpty(jsonText))
                throw new InvalidDataException("GLB container does not contain a valid JSON chunk.");

            return LoadGltf(jsonText!, binBuffer, modelName);
        }

        /// <summary>
        /// Loads a glTF 2.0 text file from disk into a hierarchical <see cref="SceneNode"/>.
        /// External binary buffer files referenced by relative URI will be automatically loaded from the same directory.
        /// </summary>
        public static SceneNode LoadGltfFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"glTF file not found: {filePath}", filePath);

            string jsonText = File.ReadAllText(filePath);
            string baseDir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(filePath);

            byte[]? externalBuffer = null;

            var jsonRoot = MiniJson.Parse(jsonText) as Dictionary<string, object?>;
            if (jsonRoot != null && jsonRoot.TryGetValue("buffers", out var buffersObj) && buffersObj is List<object?> bufferList && bufferList.Count > 0)
            {
                if (bufferList[0] is Dictionary<string, object?> firstBuf && firstBuf.TryGetValue("uri", out var uriObj) && uriObj is string uriStr)
                {
                    if (uriStr.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = uriStr.IndexOf(',');
                        if (comma >= 0)
                        {
                            string base64 = uriStr.Substring(comma + 1);
                            externalBuffer = Convert.FromBase64String(base64);
                        }
                    }
                    else
                    {
                        string binPath = Path.Combine(baseDir, uriStr);
                        if (File.Exists(binPath))
                        {
                            externalBuffer = File.ReadAllBytes(binPath);
                        }
                    }
                }
            }

            return LoadGltf(jsonText, externalBuffer, name);
        }

        /// <summary>
        /// Parses a glTF 2.0 JSON string and associated binary buffer into a hierarchical <see cref="SceneNode"/>.
        /// </summary>
        public static SceneNode LoadGltf(string jsonText, byte[]? binBuffer = null, string modelName = "GltfModel")
        {
            if (string.IsNullOrEmpty(jsonText))
                throw new ArgumentNullException(nameof(jsonText));

            var rootObj = MiniJson.Parse(jsonText) as Dictionary<string, object?>;
            if (rootObj == null)
                throw new InvalidDataException("Failed to parse glTF JSON content.");

            if (binBuffer == null && rootObj.TryGetValue("buffers", out var bufObj) && bufObj is List<object?> bufList && bufList.Count > 0)
            {
                if (bufList[0] is Dictionary<string, object?> b && b.TryGetValue("uri", out var u) && u is string uri)
                {
                    if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        int comma = uri.IndexOf(',');
                        if (comma >= 0)
                        {
                            binBuffer = Convert.FromBase64String(uri.Substring(comma + 1));
                        }
                    }
                }
            }

            var bufferViews = ParseBufferViews(rootObj);
            var accessors = ParseAccessors(rootObj);
            var meshes = ParseMeshes(rootObj, accessors, bufferViews, binBuffer);
            var nodes = ParseNodes(rootObj, meshes);

            var rootNode = new SceneNode { Name = modelName };

            int sceneIdx = 0;
            if (rootObj.TryGetValue("scene", out var sc) && sc is double scNum)
            {
                sceneIdx = (int)scNum;
            }

            var rootNodeIndices = new List<int>();
            if (rootObj.TryGetValue("scenes", out var scenesObj) && scenesObj is List<object?> scenesList && sceneIdx < scenesList.Count)
            {
                if (scenesList[sceneIdx] is Dictionary<string, object?> activeScene &&
                    activeScene.TryGetValue("nodes", out var scNodes) && scNodes is List<object?> scNodeList)
                {
                    foreach (var n in scNodeList)
                    {
                        if (n is double num) rootNodeIndices.Add((int)num);
                    }
                }
            }

            if (rootNodeIndices.Count == 0)
            {
                var childSet = new HashSet<int>();
                if (rootObj.TryGetValue("nodes", out var allNodesObj) && allNodesObj is List<object?> allNodesList)
                {
                    for (int i = 0; i < allNodesList.Count; i++)
                    {
                        if (allNodesList[i] is Dictionary<string, object?> nDict && nDict.TryGetValue("children", out var chObj) && chObj is List<object?> chList)
                        {
                            foreach (var c in chList)
                            {
                                if (c is double cNum) childSet.Add((int)cNum);
                            }
                        }
                    }

                    for (int i = 0; i < allNodesList.Count; i++)
                    {
                        if (!childSet.Contains(i)) rootNodeIndices.Add(i);
                    }
                }
            }

            foreach (var idx in rootNodeIndices)
            {
                if (nodes.TryGetValue(idx, out var node))
                {
                    rootNode.AddChild(node);
                }
            }

            return rootNode;
        }

        #region Internal Parsing Helpers

        private class BufferView
        {
            public int BufferIndex;
            public int ByteOffset;
            public int ByteLength;
            public int ByteStride;
        }

        private class Accessor
        {
            public int BufferViewIndex;
            public int ByteOffset;
            public int ComponentType;
            public int Count;
            public string Type = "SCALAR";
        }

        private static List<BufferView> ParseBufferViews(Dictionary<string, object?> root)
        {
            var result = new List<BufferView>();
            if (root.TryGetValue("bufferViews", out var bvObj) && bvObj is List<object?> bvList)
            {
                foreach (var item in bvList)
                {
                    if (item is Dictionary<string, object?> dict)
                    {
                        var bv = new BufferView();
                        if (dict.TryGetValue("buffer", out var b) && b is double bNum) bv.BufferIndex = (int)bNum;
                        if (dict.TryGetValue("byteOffset", out var bo) && bo is double boNum) bv.ByteOffset = (int)boNum;
                        if (dict.TryGetValue("byteLength", out var bl) && bl is double blNum) bv.ByteLength = (int)blNum;
                        if (dict.TryGetValue("byteStride", out var bs) && bs is double bsNum) bv.ByteStride = (int)bsNum;
                        result.Add(bv);
                    }
                }
            }
            return result;
        }

        private static List<Accessor> ParseAccessors(Dictionary<string, object?> root)
        {
            var result = new List<Accessor>();
            if (root.TryGetValue("accessors", out var accObj) && accObj is List<object?> accList)
            {
                foreach (var item in accList)
                {
                    if (item is Dictionary<string, object?> dict)
                    {
                        var acc = new Accessor();
                        if (dict.TryGetValue("bufferView", out var bv) && bv is double bvNum) acc.BufferViewIndex = (int)bvNum;
                        if (dict.TryGetValue("byteOffset", out var bo) && bo is double boNum) acc.ByteOffset = (int)boNum;
                        if (dict.TryGetValue("componentType", out var ct) && ct is double ctNum) acc.ComponentType = (int)ctNum;
                        if (dict.TryGetValue("count", out var c) && c is double cNum) acc.Count = (int)cNum;
                        if (dict.TryGetValue("type", out var t) && t is string tStr) acc.Type = tStr;
                        result.Add(acc);
                    }
                }
            }
            return result;
        }

        private static Dictionary<int, Mesh3D> ParseMeshes(
            Dictionary<string, object?> root,
            List<Accessor> accessors,
            List<BufferView> bufferViews,
            byte[]? binBuffer)
        {
            var result = new Dictionary<int, Mesh3D>();
            if (binBuffer == null || !root.TryGetValue("meshes", out var meshObj) || !(meshObj is List<object?> meshList))
                return result;

            for (int mIdx = 0; mIdx < meshList.Count; mIdx++)
            {
                if (meshList[mIdx] is not Dictionary<string, object?> mDict) continue;

                var combinedMesh = new Mesh3D();

                if (mDict.TryGetValue("primitives", out var primObj) && primObj is List<object?> primList)
                {
                    foreach (var prim in primList)
                    {
                        if (prim is not Dictionary<string, object?> pDict) continue;

                        if (!pDict.TryGetValue("attributes", out var attrObj) || attrObj is not Dictionary<string, object?> attrs)
                            continue;

                        List<Vec3>? positions = null;
                        if (attrs.TryGetValue("POSITION", out var posIdxObj) && posIdxObj is double pAccIdx && (int)pAccIdx < accessors.Count)
                        {
                            positions = ReadVec3Array(accessors[(int)pAccIdx], bufferViews, binBuffer);
                        }

                        if (positions == null || positions.Count == 0) continue;

                        List<Vec3>? normals = null;
                        if (attrs.TryGetValue("NORMAL", out var normIdxObj) && normIdxObj is double nAccIdx && (int)nAccIdx < accessors.Count)
                        {
                            normals = ReadVec3Array(accessors[(int)nAccIdx], bufferViews, binBuffer);
                        }

                        List<Vec2>? uvs = null;
                        if (attrs.TryGetValue("TEXCOORD_0", out var uvIdxObj) && uvIdxObj is double uvAccIdx && (int)uvAccIdx < accessors.Count)
                        {
                            uvs = ReadVec2Array(accessors[(int)uvAccIdx], bufferViews, binBuffer);
                        }

                        int vertexOffset = combinedMesh.Vertices.Count;
                        for (int i = 0; i < positions.Count; i++)
                        {
                            var pos = positions[i];
                            var norm = (normals != null && i < normals.Count) ? normals[i] : Vec3.UnitY;
                            var uv = (uvs != null && i < uvs.Count) ? uvs[i] : Vec2.Zero;
                            combinedMesh.Vertices.Add(new Vertex3D(pos, norm, uv));
                        }

                        if (pDict.TryGetValue("indices", out var indIdxObj) && indIdxObj is double indAccIdx && (int)indAccIdx < accessors.Count)
                        {
                            var indices = ReadIndexArray(accessors[(int)indAccIdx], bufferViews, binBuffer);
                            foreach (var idx in indices)
                            {
                                combinedMesh.Indices.Add(vertexOffset + idx);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < positions.Count; i++)
                            {
                                combinedMesh.Indices.Add(vertexOffset + i);
                            }
                        }
                    }
                }

                combinedMesh.ComputeBounds();
                result[mIdx] = combinedMesh;
            }

            return result;
        }

        private static Dictionary<int, SceneNode> ParseNodes(Dictionary<string, object?> root, Dictionary<int, Mesh3D> meshes)
        {
            var nodeMap = new Dictionary<int, SceneNode>();
            if (!root.TryGetValue("nodes", out var nodeObj) || !(nodeObj is List<object?> nodeList))
                return nodeMap;

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i] is not Dictionary<string, object?> nDict) continue;

                var node = new SceneNode
                {
                    Name = nDict.TryGetValue("name", out var n) && n is string nameStr ? nameStr : $"Node_{i}"
                };

                if (nDict.TryGetValue("mesh", out var m) && m is double mIdx && meshes.TryGetValue((int)mIdx, out var mesh))
                {
                    node.Mesh = mesh;
                }

                if (nDict.TryGetValue("matrix", out var matObj) && matObj is List<object?> matList && matList.Count >= 16)
                {
                    float[] m16 = new float[16];
                    for (int mi = 0; mi < 16; mi++)
                    {
                        m16[mi] = matList[mi] is double d ? (float)d : 0f;
                    }

                    node.LocalTransform = new Mat4
                    {
                        M11 = m16[0], M12 = m16[1], M13 = m16[2], M14 = m16[3],
                        M21 = m16[4], M22 = m16[5], M23 = m16[6], M24 = m16[7],
                        M31 = m16[8], M32 = m16[9], M33 = m16[10], M34 = m16[11],
                        M41 = m16[12], M42 = m16[13], M43 = m16[14], M44 = m16[15]
                    };
                }
                else
                {
                    Mat4 scale = Mat4.Identity;
                    if (nDict.TryGetValue("scale", out var scObj) && scObj is List<object?> scList && scList.Count >= 3)
                    {
                        scale = Mat4.CreateScale(
                            scList[0] is double sx ? (float)sx : 1f,
                            scList[1] is double sy ? (float)sy : 1f,
                            scList[2] is double sz ? (float)sz : 1f
                        );
                    }

                    Mat4 rot = Mat4.Identity;
                    if (nDict.TryGetValue("rotation", out var rotObj) && rotObj is List<object?> rList && rList.Count >= 4)
                    {
                        rot = Mat4.CreateFromQuaternion(
                            rList[0] is double rx ? (float)rx : 0f,
                            rList[1] is double ry ? (float)ry : 0f,
                            rList[2] is double rz ? (float)rz : 0f,
                            rList[3] is double rw ? (float)rw : 1f
                        );
                    }

                    Mat4 trans = Mat4.Identity;
                    if (nDict.TryGetValue("translation", out var trObj) && trObj is List<object?> tList && tList.Count >= 3)
                    {
                        trans = Mat4.CreateTranslation(
                            tList[0] is double tx ? (float)tx : 0f,
                            tList[1] is double ty ? (float)ty : 0f,
                            tList[2] is double tz ? (float)tz : 0f
                        );
                    }

                    node.LocalTransform = scale * rot * trans;
                }

                nodeMap[i] = node;
            }

            for (int i = 0; i < nodeList.Count; i++)
            {
                if (nodeList[i] is Dictionary<string, object?> nDict &&
                    nDict.TryGetValue("children", out var chObj) && chObj is List<object?> chList &&
                    nodeMap.TryGetValue(i, out var parentNode))
                {
                    foreach (var childItem in chList)
                    {
                        if (childItem is double cIdx && nodeMap.TryGetValue((int)cIdx, out var childNode))
                        {
                            parentNode.AddChild(childNode);
                        }
                    }
                }
            }

            return nodeMap;
        }

        private static List<Vec3> ReadVec3Array(Accessor acc, List<BufferView> bufferViews, byte[] binBuffer)
        {
            var list = new List<Vec3>(acc.Count);
            if (acc.BufferViewIndex >= bufferViews.Count) return list;

            var bv = bufferViews[acc.BufferViewIndex];
            int startOffset = bv.ByteOffset + acc.ByteOffset;
            int stride = bv.ByteStride > 0 ? bv.ByteStride : 12;

            for (int i = 0; i < acc.Count; i++)
            {
                int curr = startOffset + i * stride;
                if (curr + 12 > binBuffer.Length) break;

                float x = BitConverter.ToSingle(binBuffer, curr);
                float y = BitConverter.ToSingle(binBuffer, curr + 4);
                float z = BitConverter.ToSingle(binBuffer, curr + 8);
                list.Add(new Vec3(x, y, z));
            }

            return list;
        }

        private static List<Vec2> ReadVec2Array(Accessor acc, List<BufferView> bufferViews, byte[] binBuffer)
        {
            var list = new List<Vec2>(acc.Count);
            if (acc.BufferViewIndex >= bufferViews.Count) return list;

            var bv = bufferViews[acc.BufferViewIndex];
            int startOffset = bv.ByteOffset + acc.ByteOffset;
            int stride = bv.ByteStride > 0 ? bv.ByteStride : 8;

            for (int i = 0; i < acc.Count; i++)
            {
                int curr = startOffset + i * stride;
                if (curr + 8 > binBuffer.Length) break;

                float u = BitConverter.ToSingle(binBuffer, curr);
                float v = BitConverter.ToSingle(binBuffer, curr + 4);
                list.Add(new Vec2(u, v));
            }

            return list;
        }

        private static List<int> ReadIndexArray(Accessor acc, List<BufferView> bufferViews, byte[] binBuffer)
        {
            var list = new List<int>(acc.Count);
            if (acc.BufferViewIndex >= bufferViews.Count) return list;

            var bv = bufferViews[acc.BufferViewIndex];
            int startOffset = bv.ByteOffset + acc.ByteOffset;

            for (int i = 0; i < acc.Count; i++)
            {
                switch (acc.ComponentType)
                {
                    case 5121:
                        int bOff = startOffset + i;
                        if (bOff < binBuffer.Length) list.Add(binBuffer[bOff]);
                        break;

                    case 5123:
                        int sOff = startOffset + i * 2;
                        if (sOff + 2 <= binBuffer.Length) list.Add(BitConverter.ToUInt16(binBuffer, sOff));
                        break;

                    case 5125:
                        int iOff = startOffset + i * 4;
                        if (iOff + 4 <= binBuffer.Length) list.Add((int)BitConverter.ToUInt32(binBuffer, iOff));
                        break;
                }
            }

            return list;
        }

        #endregion

        #region Minimalist Pure C# JSON Parser

        private static class MiniJson
        {
            public static object? Parse(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;
                int index = 0;
                return ParseValue(json, ref index);
            }

            private static object? ParseValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;

                char c = json[index];
                if (c == '{') return ParseObject(json, ref index);
                if (c == '[') return ParseArray(json, ref index);
                if (c == '"') return ParseString(json, ref index);
                if (c == 't' || c == 'f') return ParseBool(json, ref index);
                if (c == 'n') return ParseNull(json, ref index);
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(json, ref index);

                return null;
            }

            private static Dictionary<string, object?> ParseObject(string json, ref int index)
            {
                var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                index++;

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == '}') { index++; return dict; }

                    string key = ParseString(json, ref index);
                    SkipWhitespace(json, ref index);

                    if (index < json.Length && json[index] == ':') index++;
                    SkipWhitespace(json, ref index);

                    object? value = ParseValue(json, ref index);
                    dict[key] = value;

                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                    else if (index < json.Length && json[index] == '}')
                    {
                        index++;
                        return dict;
                    }
                }

                return dict;
            }

            private static List<object?> ParseArray(string json, ref int index)
            {
                var list = new List<object?>();
                index++;

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == ']') { index++; return list; }

                    object? value = ParseValue(json, ref index);
                    list.Add(value);

                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                    else if (index < json.Length && json[index] == ']')
                    {
                        index++;
                        return list;
                    }
                }

                return list;
            }

            private static string ParseString(string json, ref int index)
            {
                if (json[index] == '"') index++;
                var sb = new StringBuilder();

                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\' && index < json.Length)
                    {
                        char escaped = json[index++];
                        switch (escaped)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u' when index + 4 <= json.Length:
                                string hex = json.Substring(index, 4);
                                index += 4;
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                }
                                break;
                            default:
                                sb.Append(escaped);
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            private static double ParseNumber(string json, ref int index)
            {
                int start = index;
                while (index < json.Length)
                {
                    char c = json[index];
                    if ((c >= '0' && c <= '9') || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E')
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }

                string numStr = json.Substring(start, index - start);
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
                return 0.0;
            }

            private static bool ParseBool(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("true", StringComparison.OrdinalIgnoreCase))
                {
                    index += 4;
                    return true;
                }
                if (json.Substring(index).StartsWith("false", StringComparison.OrdinalIgnoreCase))
                {
                    index += 5;
                    return false;
                }
                index++;
                return false;
            }

            private static object? ParseNull(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("null", StringComparison.OrdinalIgnoreCase))
                {
                    index += 4;
                }
                else
                {
                    index++;
                }
                return null;
            }

            private static void SkipWhitespace(string json, ref int index)
            {
                while (index < json.Length && char.IsWhiteSpace(json[index]))
                {
                    index++;
                }
            }
        }

        #endregion
    }
}
