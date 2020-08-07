using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImageSim.Attached
{
    public static class TreeViewExt
    {
        public static object GetSelectedItem(DependencyObject obj)
        {
            return (object)obj.GetValue(SelectedItemProperty);
        }

        public static void SetSelectedItem(DependencyObject obj, object value)
        {
            obj.SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.RegisterAttached("SelectedItem", typeof(object), typeof(TreeViewExt),
                new FrameworkPropertyMetadata(null, HandleSelectedItemChanged));

        private static void HandleSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView view)
            {
                var item = GetTreeViewItem(view, e.NewValue);
                item?.BringIntoView();
            }
        }

        private static T FindVisualChild<T>(Visual visual) where T : Visual
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                var child = VisualTreeHelper.GetChild(visual, i);
                if (child is Visual vChild)
                {
                    if (vChild is T ret)
                        return ret;

                    var descendant = FindVisualChild<T>(vChild);
                    if (descendant is T dret)
                        return dret;
                }
            }
            return null;
        }

        private static TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container == null)
                return null;

            if (container.DataContext.Equals(item))
                return container as TreeViewItem;
            //Expand the current container 
            if (container is TreeViewItem containerItem && !containerItem.IsExpanded)
                containerItem.IsExpanded = true;
            // Try to generate the ItemsPresenter and the ItemsPanel. 
            // by calling ApplyTemplate. Note that in the 
            // virtualizing case, even if IsExpanded = true, 
            // we still need to do this step in order to 
            // regenerate the visuals because they may have been virtualized away. 
            container.ApplyTemplate();
            if (container.Template.FindName("ItemsHost", container) is ItemsPresenter itemsPresenter)
            {
                itemsPresenter.ApplyTemplate();
            }
            else
            {
                // The Tree template has not named the ItemsPresenter, 
                // so walk the descendents and find the child. 
                itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                if (itemsPresenter == null)
                {
                    container.UpdateLayout();
                    itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                }
            }
            var itemsHostPanel = VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel;
            //Do this to ensure that the generator for this panel has been created.
            var children = itemsHostPanel.Children;
            var virtualizingPanel = itemsHostPanel as VirtualizingStackPanel;
            for (int index = 0; index < container.Items.Count; index++)
            {
                TreeViewItem subContainer;
                if (virtualizingPanel != null)
                {
                    // Bring the item into view so 
                    // that the container will be generated. 
                    virtualizingPanel.BringIndexIntoViewPublic(index);
                    subContainer = container.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                }
                else
                {
                    subContainer = container.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
                    // Bring the item into view to maintain the 
                    // same behavior as with a virtualizing panel. 
                    subContainer.BringIntoView();
                }

                if (subContainer != null)
                {
                    // Search the next level for the object.
                    var resultContainer = GetTreeViewItem(subContainer, item);
                    if (resultContainer != null)
                    {
                        return resultContainer;
                    }
                    else
                    {
                        // The object is not under this TreeViewItem so collapse it.
                        subContainer.IsExpanded = false;
                    }
                }
            }
            return null;
        }
    }
}
