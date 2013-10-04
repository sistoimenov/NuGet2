using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace NuGet.VisualStudio
{
    public static class UriHelper
    {
        public static void OpenExternalLink(Uri url)
        {
            if (url == null || !url.IsAbsoluteUri)
            {
                return;
            }

            // mitigate security risk
            if (url.IsFile || url.IsLoopback || url.IsUnc)
            {
                return;
            }

            if (IsHttpUrl(url))
            {
                // REVIEW: Will this allow a package author to execute arbitrary program on user's machine?
                // We have limited the url to be HTTP only, but is it sufficient?
                System.Diagnostics.Process.Start(url.AbsoluteUri);
                NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.LinkOpened);
            }
        }

        public static bool IsHttpSource(string source)
        {
            if (String.IsNullOrEmpty(source))
            {
                return false;
            }

            Uri uri;
            if (Uri.TryCreate(source, UriKind.Absolute, out uri))
            {
                return IsHttpUrl(uri);
            }
            else
            {
                return false;
            }
        }

        public static bool IsHttpSource(IVsPackageSourceProvider packageSourceProvider)
        {
            var activeSource = packageSourceProvider.ActivePackageSource;
            if (activeSource == null)
            {
                return false;
            }

            if (activeSource.IsAggregate())
            {
                return packageSourceProvider.GetEnabledPackageSources().Any(s => IsHttpSource(s.Source));
            }
            else
            {
                return IsHttpSource(activeSource.Source);
            }
        }

        public static bool IsHttpSource(string source, IVsPackageSourceProvider packageSourceProvider)
        {
            if (source != null)
            {
                if (IsHttpSource(source))
                {
                    return true;
                }

                var packageSource = packageSourceProvider.GetEnabledPackageSourcesWithAggregate()
                                                          .FirstOrDefault(p => p.Name.Equals(source, StringComparison.CurrentCultureIgnoreCase));
                return (packageSource != null) && IsHttpSource(packageSource.Source);
            }

            return IsHttpSource(packageSourceProvider);
        }

        private static bool IsHttpUrl(Uri uri)
        {
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsLocalOrUncSharePresent(string currentSource)
        {
            Uri currentURI = new Uri(currentSource);

            if (currentURI.HostNameType == UriHostNameType.Basic || currentURI.IsUnc)
            {
                if (Directory.Exists(currentSource))
                {
                    return true;
                }
            }
            return false;
        }
        
        public static bool IsAnySourceAvailable(string source, IVsPackageSourceProvider packageSourceProvider, bool checkHttp)
        {           
            //If the source switch or any of the enabled sources is http, return true
            if (checkHttp)
            {
                bool isHttpSource;
                isHttpSource = (packageSourceProvider == null)? UriHelper.IsHttpSource(source): UriHelper.IsHttpSource(source, packageSourceProvider);
                 if (isHttpSource)
                {
                    return true;
                }
            }

            //If directory exists for -Source, return true;
            if (Directory.Exists(source))
            {
                return true;
            }

            if (packageSourceProvider != null)
            {
                //If any of the active sources is UNC share or local folder and is available, return true
                IEnumerable<PackageSource> sources = null;
                PackageSource activeSource = packageSourceProvider.ActivePackageSource;
                if (activeSource.IsAggregate())
                {
                    sources = packageSourceProvider.GetEnabledPackageSources();
                    foreach (PackageSource s in sources)
                    {
                        if (IsLocalOrUncSharePresent(s.Source)) return true;
                    }
                }
                else
                {
                    if (IsLocalOrUncSharePresent(activeSource.Source)) return true;
                }
            }

            //If none of the above matched, return false
            return false;
        }
    }
}