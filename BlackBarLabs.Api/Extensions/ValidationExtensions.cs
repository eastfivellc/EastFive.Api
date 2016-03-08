using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Web.Mvc;

namespace BlackBarLabs.Api
{
    public static class ValidationExtensions
    {
        public static Exception PreconditionViewModelEntityAlreadyExists<TViewModel>(this TViewModel viewModel)
        {
            return new Exception("An entity with the requested id already exists. ");
        }

        public static Exception PreconditionViewModelEntityNotFound<TViewModel>(this TViewModel viewModel)
        {
            return new Exception("An entity with the requested id was not found. ");
        }

        public static Exception PreconditionViewModelRequestTypeNotSupported<TViewModel>(this TViewModel viewModel, string message = null)
        {
            return new Exception("The request type on this entity is not supported. " + (string.IsNullOrEmpty(message) ? string.Empty : "Detail: " + message));
        }

        public static Exception PreconditionInvalidParameter<TController, TReturn>(this TController controller, Expression<Func<TController, TReturn>> controllerCall, string message, string parameterName)
        {
            MethodCallExpression mce = (MethodCallExpression)controllerCall.Body;
            foreach (var arg in mce.Arguments)
            {
                MemberExpression me = arg as MemberExpression;
                if (me != null && me.Member.Name == parameterName)
                {
                    return new ArgumentException(message, string.Format("{0} of type {1}", me.Member.Name, me.Type.FullName));
                }
            }
            return new ArgumentException(message, parameterName);
        }

        public static void PreconditionViewModelPropertyIsNotDefault<TReturn, TViewModel>(this TViewModel viewModel, Expression<Func<TViewModel, TReturn>> propertyExpression, string message)
        {
            var propertyValue = propertyExpression.Compile().Invoke(viewModel);
            if (EqualityComparer<TReturn>.Default.Equals(propertyValue, default(TReturn)))
            {
                throw viewModel.InvalidViewModelProperty(propertyExpression, message);
            }
        }

        public static void PreconditionViewModelPropertyIsNotDefaultOrEmpty<TReturn, TReturnType, TViewModel>(this TViewModel viewModel, Expression<Func<TViewModel, TReturn>> propertyExpression, string message)
            where TReturn : IEnumerable<TReturnType>
        {
            var propertyValue = propertyExpression.Compile().Invoke(viewModel);
            if (EqualityComparer<TReturn>.Default.Equals(propertyValue, default(TReturn)) || 
                EqualityComparer<TReturnType>.Default.Equals(default(TReturnType), propertyValue.FirstOrDefault()))
            {
                throw viewModel.InvalidViewModelProperty(propertyExpression, message);
            }
        }

        public static Exception InvalidViewModelProperty<TReturn, TViewModel>(this TViewModel viewModel, Expression<Func<TViewModel, TReturn>> propertyExpression, string message)
        {
            var lockedPropertyMember = ((MemberExpression)propertyExpression.Body).Member;
            var propertyInfo = lockedPropertyMember as PropertyInfo;
            if (null == propertyInfo)
            {
                return new ArgumentException("Property expression does not reference a property.", "propertyExpression");
            }
            return new ArgumentException(message, propertyInfo.Name);
        }
    }
}