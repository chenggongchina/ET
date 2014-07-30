﻿using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using Helper;

namespace Tree
{
    [Export(contractType: typeof (BehaviorTreeViewModel)),
     PartCreationPolicy(creationPolicy: CreationPolicy.NonShared)]
    internal class BehaviorTreeViewModel
    {
        private readonly ObservableCollection<TreeNodeViewModel> treeNodes =
                new ObservableCollection<TreeNodeViewModel>();

        public ObservableCollection<TreeNodeViewModel> TreeNodes
        {
            get
            {
                return this.treeNodes;
            }
        }

        private TreeNodeViewModel Root
        {
            get
            {
                return this.treeNodes.Count == 0? null : this.treeNodes[0];
            }
        }

        public void Add(TreeNodeViewModel treeNode, TreeNodeViewModel parent)
        {
            // 如果父节点是折叠的,需要先展开父节点
            if (parent != null && parent.IsFolder)
            {
                this.UnFold(parent);
            }
            this.treeNodes.Add(treeNode);
            if (parent != null)
            {
                parent.Children.Add(treeNode);
            }
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }

        private void RecursionRemove(TreeNodeViewModel treeNodeViewModel)
        {
            for (int i = 0; i < treeNodeViewModel.Children.Count; ++i)
            {
                this.RecursionRemove(treeNodeViewModel.Children[i]);
            }
            this.treeNodes.Remove(treeNodeViewModel);
        }

        public void Remove(TreeNodeViewModel treeNodeViewModel)
        {
            this.RecursionRemove(treeNodeViewModel);
            treeNodeViewModel.Parent.Children.Remove(treeNodeViewModel);
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }

        private void RecursionMove(
                TreeNodeViewModel treeNodeViewModel, double offsetX, double offsetY)
        {
            treeNodeViewModel.X += offsetX;
            treeNodeViewModel.Y += offsetY;
            foreach (var node in treeNodeViewModel.Children)
            {
                this.RecursionMove(node, offsetX, offsetY);
            }
        }

        public void MoveToPosition(double offsetX, double offsetY)
        {
            this.RecursionMove(this.Root, offsetX, offsetY);
        }

        public void MoveToNode(TreeNodeViewModel from, TreeNodeViewModel to)
        {
            // from节点不能是to节点的父级节点
            TreeNodeViewModel tmpNode = to;
            while (tmpNode != null)
            {
                if (tmpNode.IsRoot)
                {
                    break;
                }
                if (tmpNode.Id == from.Id)
                {
                    return;
                }
                tmpNode = tmpNode.Parent;
            }

            if (from.IsFolder)
            {
                this.UnFold(from);
            }

            if (to.IsFolder)
            {
                this.UnFold(to);
            }
            from.Parent.Children.Remove(from);
            to.Children.Add(from);
            from.Parent = to;
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }

        /// <summary>
        /// 折叠节点
        /// </summary>
        /// <param name="treeNodeViewModel"></param>
        public void Fold(TreeNodeViewModel treeNodeViewModel)
        {
            foreach (var node in treeNodeViewModel.Children)
            {
                this.RecursionRemove(node);
            }
            treeNodeViewModel.IsFolder = true;
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }

        /// <summary>
        /// 展开节点,一级一级展开,一次只展开下层子节点,比如下层节点是折叠的,那下下层节点不展开
        /// </summary>
        /// <param name="unFoldNode"></param>
        public void UnFold(TreeNodeViewModel unFoldNode)
        {
            foreach (var tn in unFoldNode.Children)
            {
                this.RecursionAdd(tn);
            }
            unFoldNode.IsFolder = false;
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }

        private void RecursionAdd(TreeNodeViewModel treeNodeViewModel)
        {
            if (!this.treeNodes.Contains(treeNodeViewModel))
            {
                this.treeNodes.Add(treeNodeViewModel);
            }
            ObservableCollection<TreeNodeViewModel> children = treeNodeViewModel.Children;

            if (treeNodeViewModel.IsFolder)
            {
                return;
            }
            foreach (var tn in children)
            {
                this.RecursionAdd(tn);
            }
        }

        /// <summary>
        /// 序列化保存
        /// </summary>
        public void Save(string filePath)
        {
            var treeNodeDataArray = new TreeNodeDataArray();
            this.RecursionSave(treeNodeDataArray, this.Root);
            byte[] bytes = ProtobufHelper.ToBytes(treeNodeDataArray);
            using (Stream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private void RecursionSave(TreeNodeDataArray treeNodeDataArray, TreeNodeViewModel node)
        {
            if (node == null)
            {
                return;
            }
            treeNodeDataArray.Add(node.TreeNodeData);
            foreach (TreeNodeViewModel childNode in node.Children)
            {
                this.RecursionSave(treeNodeDataArray, childNode);
            }
        }

        /// <summary>
        /// 从配置中加载
        /// </summary>
        /// <param name="filePath"></param>
        public void Load(string filePath)
        {
            this.TreeNodes.Clear();
            byte[] bytes = File.ReadAllBytes(filePath);
            var treeNodeDataArray = ProtobufHelper.FromBytes<TreeNodeDataArray>(bytes);
            treeNodeDataArray.Init();
            if (treeNodeDataArray.TreeNodeDatas.Count == 0)
            {
                return;
            }
            this.RecursionLoad(treeNodeDataArray, treeNodeDataArray.TreeNodeDatas[0], null);
        }

        private void RecursionLoad(
                TreeNodeDataArray treeNodeDataArray, TreeNodeData treeNodeData,
                TreeNodeViewModel parentNode)
        {
            var node = new TreeNodeViewModel(treeNodeData, parentNode);
            this.Add(node, parentNode);
            foreach (int id in treeNodeData.Children)
            {
                TreeNodeData childNodeData = treeNodeDataArray[id];
                this.RecursionLoad(treeNodeDataArray, childNodeData, node);
            }
            BehaviorTreeLayout.ExcuteLayout(this.Root);
        }
    }
}