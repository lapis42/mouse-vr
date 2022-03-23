﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Janelia
{
    public class PackageNode
    {
        public readonly string PkgDir;
        public readonly HashSet<PackageNode> Deps;

        public PackageNode(string pkgDir)
        {
            if (!_pkgDirToNode.ContainsKey(pkgDir))
            {
                _pkgDirToNode.Add(pkgDir, this);

                PkgDir = pkgDir;

                HashSet<string> depPkgDirs = GetDepPkgDirs(pkgDir, "Runtime");
                depPkgDirs.UnionWith(GetDepPkgDirs(pkgDir, "Editor"));

                Deps = new HashSet<PackageNode>();
                foreach (string depPkgDir in depPkgDirs)
                {
                    // Do not get into a cycle if "Editor" depends on "Runtime".
                    if (depPkgDir == pkgDir)
                    {
                        continue;
                    }

                    // An edge from node U to node V means U should preceed V in the
                    // topological sort, that is, V has a dependency on U and
                    // U must be installed before V.
                    PackageNode other = _pkgDirToNode.ContainsKey(depPkgDir) ?
                        _pkgDirToNode[depPkgDir] :
                        new PackageNode(depPkgDir);
                    other.Deps.Add(this);
                }
            }
        }

        static public List<string> TopoSort()
        {
            List<string> result = new List<string>();

            // Depth-first search gives the topological sort in reversed order.
            // So do the search and then reverse the result.
            foreach (PackageNode node in _pkgDirToNode.Values)
            {
                Visit(node);
            }
            result.Reverse();

            // Get ready for the next use.
            _pkgDirToNode.Clear();

            return result;

            void Visit(PackageNode node, int depth = 0)
            {
                // The linear performance of `Contains` is not an issue since `result` will never get large.
                if (!result.Contains(node.PkgDir))
                {
                    // Avoid catestrophic failures that would affect the user interface.
                    if (depth > 128)
                    {
                        Debug.Log("Aborted recursion in PackageNode at depth " + depth);
                        return;
                    }

                    foreach (PackageNode dep in node.Deps)
                    {
                        Visit(dep, depth + 1);
                    }
                    result.Add(node.PkgDir);
                }
            }
        }

        private HashSet<string> GetDepPkgDirs(string pkgDir, string subDir)
        {
            HashSet<string> result = new HashSet<string>();
            string rootDir = Path.GetDirectoryName(pkgDir);
            string dir = pkgDir + Path.DirectorySeparatorChar + subDir;
            if (System.IO.Directory.Exists(dir))
            {
                string[] asmdefPaths = System.IO.Directory.GetFiles(dir, "*.asmdef");
                foreach (string asmdefPath in asmdefPaths)
                {
                    string asmdefContents = System.IO.File.ReadAllText(asmdefPath);
                    Asmdef asmdef = JsonUtility.FromJson<Asmdef>(asmdefContents);
                    foreach (string reference in asmdef.references)
                    {
                        int i = reference.LastIndexOf(".");
                        if (i == -1)
                        {
                            Debug.Log(reference + " is not a part of the current packages");
                            continue;
                        }
                        string referencePkg = reference.Substring(0, i).ToLower();
                        string referencePkgDir = rootDir + Path.DirectorySeparatorChar + "org." + referencePkg;
                        result.Add(referencePkgDir);
                    }
                }
            }
            return result;
        }

        // A struct with the part of the .asmdef JSON file that needs to be parsed to determine dependencies.
        [Serializable]
        private struct Asmdef
        {
            public string[] references;

            // This constructor prevents a spurious compiler warning about `references` never getting assigned.
            Asmdef(string[] refs = null)
            {
                references = refs;
            }
        };

        private static Dictionary<string, PackageNode> _pkgDirToNode = new Dictionary<string, PackageNode>();
    }
}