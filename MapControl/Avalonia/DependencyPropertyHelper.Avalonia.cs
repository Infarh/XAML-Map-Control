﻿// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// Copyright © 2024 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using Avalonia.Controls;
using System;

#pragma warning disable AVP1001 // The same AvaloniaProperty should not be registered twice

namespace MapControl
{
    public static class DependencyPropertyHelper
    {
        public static StyledProperty<TValue> Register<TOwner, TValue>(
            string name,
            TValue defaultValue = default,
            Action<TOwner, TValue, TValue> changed = null,
            Func<TOwner, TValue, TValue> coerce = null,
            bool bindTwoWayByDefault = false)
            where TOwner : AvaloniaObject
        {
            var property = AvaloniaProperty.Register<TOwner, TValue>(name, defaultValue, false,
                bindTwoWayByDefault ? Avalonia.Data.BindingMode.TwoWay : Avalonia.Data.BindingMode.OneWay, null,
                coerce != null ? ((obj, value) => coerce((TOwner)obj, value)) : null);

            if (changed != null)
            {
                property.Changed.AddClassHandler<TOwner, TValue>((o, e) => changed(o, e.OldValue.Value, e.NewValue.Value));
            }

            return property;
        }

        public static AttachedProperty<TValue> RegisterAttached<TOwner, TValue>(
            string name,
            TValue defaultValue = default,
            Action<Control, TValue, TValue> changed = null,
            bool inherits = false)
        {
            var property = AvaloniaProperty.RegisterAttached<TOwner, Control, TValue>(name, defaultValue, inherits);

            if (changed != null)
            {
                property.Changed.AddClassHandler<Control, TValue>((o, e) => changed(o, e.OldValue.Value, e.NewValue.Value));
            }

            return property;
        }
    }
}
