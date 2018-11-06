using System;
using System.Collections.Generic;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace UnityEditor.Experimental.Rendering
{
    [Flags]
    public enum FoldoutOption
    {
        None = 0,
        Indent = 1 << 0,
        [Obsolete("animated inspector does not been approved by UX team")]
        Animate = 1 << 1,   
        Boxed = 1 << 2
    }

    [Flags]
    [Obsolete("Will disappear with CoreEditorDrawer<TUIState, TData>")]
    public enum FadeOption
    {
        None = 0,
        Indent = 1 << 0,
        Animate = 1 << 1
    }

    [Obsolete("Prefer using the version without the UIState in coordination with a static ExpandedState")]
    public static class CoreEditorDrawer<TUIState, TData>
    {
        public interface IDrawer
        {
            void Draw(TUIState s, TData p, Editor owner);
        }

        public delegate T2UIState StateSelect<T2UIState>(TUIState s, TData d, Editor o);
        public delegate T2Data DataSelect<T2Data>(TUIState s, TData d, Editor o);

        public delegate void ActionDrawer(TUIState s, TData p, Editor owner);
        public delegate AnimBool AnimBoolItemGetter(TUIState s, TData p, Editor owner, int i);
        public delegate AnimBool AnimBoolGetter(TUIState s, TData p, Editor owner);

        public static readonly IDrawer space = Action((state, data, owner) => EditorGUILayout.Space());
        public static readonly IDrawer noop = Action((state, data, owner) => {});

        public static IDrawer Group(params IDrawer[] drawers)
        {
            return new GroupDrawerInternal(drawers);
        }

        public static IDrawer LabelWidth(float width, params IDrawer[] drawers)
        {
            return Action((s, d, o) =>
                {
                    var l = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = width;
                    for (var i = 0; i < drawers.Length; ++i)
                        drawers[i].Draw(s, d, o);
                    EditorGUIUtility.labelWidth = l;
                }
                );
        }

        public static IDrawer Action(params ActionDrawer[] drawers)
        {
            return new ActionDrawerInternal(drawers);
        }

        public static IDrawer FadeGroup(AnimBoolItemGetter fadeGetter, FadeOption options, params IDrawer[] groupDrawers)
        {
            return new FadeGroupsDrawerInternal(fadeGetter, options, groupDrawers);
        }

        public static IDrawer FoldoutGroup(string title, AnimBoolGetter root, FoldoutOption options, params IDrawer[] bodies)
        {
            return new FoldoutDrawerInternal(title, root, options, bodies);
        }

        public static IDrawer Select<T2UIState, T2Data>(
            StateSelect<T2UIState> stateSelect,
            DataSelect<T2Data> dataSelect,
            params CoreEditorDrawer<T2UIState, T2Data>.IDrawer[] otherDrawers)
        {
            return new SelectDrawerInternal<T2UIState, T2Data>(stateSelect, dataSelect, otherDrawers);
        }

        class GroupDrawerInternal : IDrawer
        {
            IDrawer[] drawers { get; set; }
            public GroupDrawerInternal(params IDrawer[] drawers)
            {
                this.drawers = drawers;
            }

            void IDrawer.Draw(TUIState s, TData p, Editor owner)
            {
                for (var i = 0; i < drawers.Length; i++)
                    drawers[i].Draw(s, p, owner);
            }
        }

        class SelectDrawerInternal<T2UIState, T2Data> : IDrawer
        {
            StateSelect<T2UIState> m_StateSelect;
            DataSelect<T2Data> m_DataSelect;
            CoreEditorDrawer<T2UIState, T2Data>.IDrawer[] m_SourceDrawers;

            public SelectDrawerInternal(StateSelect<T2UIState> stateSelect,
                                        DataSelect<T2Data> dataSelect,
                                        params CoreEditorDrawer<T2UIState, T2Data>.IDrawer[] otherDrawers)
            {
                m_SourceDrawers = otherDrawers;
                m_StateSelect = stateSelect;
                m_DataSelect = dataSelect;
            }

            void IDrawer.Draw(TUIState s, TData p, Editor o)
            {
                var s2 = m_StateSelect(s, p, o);
                var p2 = m_DataSelect(s, p, o);
                for (var i = 0; i < m_SourceDrawers.Length; i++)
                    m_SourceDrawers[i].Draw(s2, p2, o);
            }
        }

        class ActionDrawerInternal : IDrawer
        {
            ActionDrawer[] actionDrawers { get; set; }
            public ActionDrawerInternal(params ActionDrawer[] actionDrawers)
            {
                this.actionDrawers = actionDrawers;
            }

            void IDrawer.Draw(TUIState s, TData p, Editor owner)
            {
                for (var i = 0; i < actionDrawers.Length; i++)
                    actionDrawers[i](s, p, owner);
            }
        }

        class FadeGroupsDrawerInternal : IDrawer
        {
            IDrawer[] m_GroupDrawers;
            AnimBoolItemGetter m_Getter;
            FadeOption m_Options;

            bool indent { get { return (m_Options & FadeOption.Indent) != 0; } }
            bool animate { get { return (m_Options & FadeOption.Animate) != 0; } }

            public FadeGroupsDrawerInternal(AnimBoolItemGetter getter, FadeOption options, params IDrawer[] groupDrawers)
            {
                m_GroupDrawers = groupDrawers;
                m_Getter = getter;
                m_Options = options;
            }

            void IDrawer.Draw(TUIState s, TData p, Editor owner)
            {
                // We must start with a layout group here
                // Otherwise, nested FadeGroup won't work
                GUILayout.BeginVertical();
                for (var i = 0; i < m_GroupDrawers.Length; ++i)
                {
                    var b = m_Getter(s, p, owner, i);
                    if (animate && EditorGUILayout.BeginFadeGroup(b.faded)
                        || !animate && b.target)
                    {
                        if (indent)
                            ++EditorGUI.indentLevel;
                        m_GroupDrawers[i].Draw(s, p, owner);
                        if (indent)
                            --EditorGUI.indentLevel;
                    }
                    if (animate)
                        EditorGUILayout.EndFadeGroup();
                }
                GUILayout.EndVertical();
            }
        }

        class FoldoutDrawerInternal : IDrawer
        {
            IDrawer[] m_Bodies;
            AnimBoolGetter m_IsExpanded;
            string m_Title;
            FoldoutOption m_Options;

            bool animate { get { return (m_Options & FoldoutOption.Animate) != 0; } }
            bool indent { get { return (m_Options & FoldoutOption.Indent) != 0; } }
            bool boxed { get { return (m_Options & FoldoutOption.Boxed) != 0; } }

            public FoldoutDrawerInternal(string title, AnimBoolGetter isExpanded, FoldoutOption options, params IDrawer[] bodies)
            {
                m_Title = title;
                m_IsExpanded = isExpanded;
                m_Bodies = bodies;
                m_Options = options;
            }

            public void Draw(TUIState s, TData p, Editor owner)
            {
                var r = m_IsExpanded(s, p, owner);
                CoreEditorUtils.DrawSplitter(boxed);
                r.target = CoreEditorUtils.DrawHeaderFoldout(m_Title, r.target, boxed);
                // We must start with a layout group here
                // Otherwise, nested FadeGroup won't work
                GUILayout.BeginVertical();
                if (animate && EditorGUILayout.BeginFadeGroup(r.faded)
                    || !animate && r.target)
                {
                    if (indent)
                        ++EditorGUI.indentLevel;
                    for (var i = 0; i < m_Bodies.Length; i++)
                        m_Bodies[i].Draw(s, p, owner);
                    if (indent)
                        --EditorGUI.indentLevel;
                }
                if (animate)
                    EditorGUILayout.EndFadeGroup();
                GUILayout.EndVertical();
            }
        }
        
        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, params ActionDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return Action((uiState, data, editor) =>
            {
                CoreEditorUtils.DrawSplitter();
                bool expended = state[mask];
                bool newExpended = CoreEditorUtils.DrawHeaderFoldout(title, expended);
                if (newExpended ^ expended)
                    state[mask] = newExpended;
                if (newExpended)
                {
                    ++EditorGUI.indentLevel;
                    for (var i = 0; i < contentDrawer.Length; i++)
                        contentDrawer[i](uiState, data, editor);
                    --EditorGUI.indentLevel;
                    EditorGUILayout.Space();
                }
            });
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, params IDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, contentDrawer.Draw);
        }
    }

    /// <summary>
    /// Utility class to draw inspectors
    /// </summary>
    /// <typeparam name="TData">Type of class containing data needed to draw inspector</typeparam>
    public static class CoreEditorDrawer<TData>
    {
        /// <summary> Abstraction that have the Draw hability </summary>
        public interface IDrawer
        {
            void Draw(TData p, Editor owner);
        }
        
        public delegate T2Data DataSelect<T2Data>(TData d, Editor o);
        public delegate void ActionDrawer(TData p, Editor owner);

        /// <summary> Equivalent to EditorGUILayout.Space that can be put in a drawer group </summary>
        public static readonly IDrawer space = Group((data, owner) => EditorGUILayout.Space());
        
        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        public static IDrawer Group(params IDrawer[] drawers)
        {
            return new GroupDrawerInternal(-1f, drawers.Draw);
        }

        /// <summary>
        /// Group of drawing function for inspector.
        /// They will be drawn one after the other.
        /// </summary>
        public static IDrawer Group(params ActionDrawer[] drawers)
        {
            return new GroupDrawerInternal(-1f, drawers);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="drawers">The content of the group</param>
        public static IDrawer Group(float labelWidth, params IDrawer[] drawers)
        {
            return new GroupDrawerInternal(labelWidth, drawers.Draw);
        }

        /// <summary> Group of drawing function for inspector with a set width for labels </summary>
        /// <param name="labelWidth">Width used for all labels in the group</param>
        /// <param name="drawers">The content of the group</param>
        public static IDrawer Group(float labelWidth, params ActionDrawer[] drawers)
        {
            return new GroupDrawerInternal(labelWidth, drawers);
        }
        
        class GroupDrawerInternal : IDrawer
        {
            ActionDrawer[] actionDrawers { get; set; }
            float m_LabelWidth;
            public GroupDrawerInternal(float labelWidth = -1f, params ActionDrawer[] actionDrawers)
            {
                this.actionDrawers = actionDrawers;
                m_LabelWidth = labelWidth;
            }

            void IDrawer.Draw(TData p, Editor owner)
            {
                var currentLabelWidth = EditorGUIUtility.labelWidth;
                if (m_LabelWidth >= 0f)
                {
                    EditorGUIUtility.labelWidth = m_LabelWidth;
                }
                for (var i = 0; i < actionDrawers.Length; i++)
                    actionDrawers[i](p, owner);
                if (m_LabelWidth >= 0f)
                {
                    EditorGUIUtility.labelWidth = currentLabelWidth;
                }
            }
        }

        /// <summary> Create an IDrawer based on an other data container </summary>
        /// <param name="dataSelect">The data new source for the inner drawers</param>
        /// <param name="otherDrawers">Inner drawers drawed with given data sources</param>
        /// <returns></returns>
        public static IDrawer Select<T2Data>(
            DataSelect<T2Data> dataSelect,
            params CoreEditorDrawer<T2Data>.IDrawer[] otherDrawers)
        {
            return new SelectDrawerInternal<T2Data>(dataSelect, otherDrawers.Draw);
        }

        /// <summary> Create an IDrawer based on an other data container </summary>
        /// <param name="dataSelect">The data new source for the inner drawers</param>
        /// <param name="otherDrawers">Inner drawers drawed with given data sources</param>
        /// <returns></returns>
        public static IDrawer Select<T2Data>(
            DataSelect<T2Data> dataSelect,
            params CoreEditorDrawer<T2Data>.ActionDrawer[] otherDrawers)
        {
            return new SelectDrawerInternal<T2Data>(dataSelect, otherDrawers);
        }

        class SelectDrawerInternal<T2Data> : IDrawer
        {
            DataSelect<T2Data> m_DataSelect;
            CoreEditorDrawer<T2Data>.ActionDrawer[] m_SourceDrawers;

            public SelectDrawerInternal(DataSelect<T2Data> dataSelect,
                params CoreEditorDrawer<T2Data>.ActionDrawer[] otherDrawers)
            {
                m_SourceDrawers = otherDrawers;
                m_DataSelect = dataSelect;
            }

            void IDrawer.Draw(TData p, Editor o)
            {
                var p2 = m_DataSelect(p, o);
                for (var i = 0; i < m_SourceDrawers.Length; i++)
                    m_SourceDrawers[i](p2, o);
            }
        }
        
        /// <summary>
        /// Create an IDrawer foldout header using an ExpandedState.
        /// The default option is Indent in this version.
        /// </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, params IDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, contentDrawer.Draw);
        }

        /// <summary>
        /// Create an IDrawer foldout header using an ExpandedState.
        /// The default option is Indent in this version.
        /// </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, params ActionDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(CoreEditorUtils.GetContent(title), mask, state, contentDrawer);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, FoldoutOption options, params IDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, contentDrawer.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(string title, TEnum mask, ExpandedState<TEnum, TState> state, FoldoutOption options, params ActionDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(CoreEditorUtils.GetContent(title), mask, state, options, contentDrawer);
        }

        /// <summary>
        /// Create an IDrawer foldout header using an ExpandedState.
        /// The default option is Indent in this version.
        /// </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(GUIContent title, TEnum mask, ExpandedState<TEnum, TState> state, params IDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, contentDrawer.Draw);
        }

        /// <summary>
        /// Create an IDrawer foldout header using an ExpandedState.
        /// The default option is Indent in this version.
        /// </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(GUIContent title, TEnum mask, ExpandedState<TEnum, TState> state, params ActionDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, FoldoutOption.Indent, contentDrawer);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(GUIContent title, TEnum mask, ExpandedState<TEnum, TState> state, FoldoutOption options, params IDrawer[] contentDrawer)
            where TEnum : struct, IConvertible
        {
            return FoldoutGroup(title, mask, state, options, contentDrawer.Draw);
        }

        /// <summary> Create an IDrawer foldout header using an ExpandedState </summary>
        /// <param name="title">Title wanted for this foldout header</param>
        /// <param name="mask">Bit mask (enum) used to define the boolean saving the state in ExpandedState</param>
        /// <param name="state">The ExpandedState describing the component</param>
        /// <param name="contentDrawer">The content of the foldout header</param>
        public static IDrawer FoldoutGroup<TEnum, TState>(GUIContent title, TEnum mask, ExpandedState<TEnum, TState> state, FoldoutOption options, params ActionDrawer[] contentDrawer)
        where TEnum : struct, IConvertible
        {
            return Group((data, editor) =>
            {
                CoreEditorUtils.DrawSplitter();
                bool expended = state[mask];
                bool isBoxed = (options & FoldoutOption.Boxed) != 0;
                bool isIndented = (options & FoldoutOption.Indent) != 0;
                bool newExpended = CoreEditorUtils.DrawHeaderFoldout(title, expended, isBoxed);
                if (newExpended ^ expended)
                    state[mask] = newExpended;
                if (newExpended)
                {
                    if(isIndented)
                        ++EditorGUI.indentLevel;
                    for (var i = 0; i < contentDrawer.Length; i++)
                        contentDrawer[i](data, editor);
                    if(isIndented)
                        --EditorGUI.indentLevel;
                    EditorGUILayout.Space();
                }
            });
        }
    }

    public static class CoreEditorDrawersExtensions
    {
        [Obsolete]
        public static void Draw<TUIState, TData>(this IEnumerable<CoreEditorDrawer<TUIState, TData>.IDrawer> drawers, TUIState s, TData p, Editor o)
        {
            foreach (var drawer in drawers)
                drawer.Draw(s, p, o);
        }
        
        public static void Draw<TData>(this IEnumerable<CoreEditorDrawer<TData>.IDrawer> drawers, TData p, Editor o)
        {
            foreach (var drawer in drawers)
                drawer.Draw(p, o);
        }
    }
}
