#if DISABLE_XAML_GENERATED_MAIN
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Unigram
{
    public static class Program
    {
        // This example code shows how you could implement the required Main method to
        // support multi-instance redirection. The minimum requirement is to call
        // Application.Start with a new App object. Beyond that, you may delete the
        // rest of the example code and replace it with your custom code if you wish.
        static int _lastId = 0;

        static void Main(string[] args)
        {
            // First, we'll get our activation event args, which are typically richer
            // than the incoming command-line args. We can use these in our app-defined
            // logic for generating the key for this instance.
            var activatedArgs = AppInstance.GetActivatedEventArgs();
            if (activatedArgs is CommandLineActivatedEventArgs commandLine && TryParseCommandLine(commandLine, out int id, out bool test))
            {
                _lastId = id;

                var instance = AppInstance.FindOrRegisterInstanceForKey(id.ToString());
                if (instance.IsCurrentInstance)
                {
                    // If we successfully registered this instance, we can now just
                    // go ahead and do normal XAML initialization.
                    global::Windows.UI.Xaml.Application.Start((p) => new App(id));
                }
                else
                {
                    // Some other instance has registered for this key, so we'll 
                    // redirect this activation to that instance instead.
                    instance.RedirectActivationTo();
                }
            }
            else
            {
                // The platform might provide a recommended instance.
                if (AppInstance.RecommendedInstance != null)
                {
                    AppInstance.RecommendedInstance.RedirectActivationTo();
                }
                else
                {
                    // If the platform hasn't expressed a preference, we need to examine all
                    // other instances to see if any are suitable for redirecting this request.
                    // In the simple case, any instance will do.
                    //AppInstance instance = instances.FirstOrDefault();

                    // If the app re-registers re-usable instances, we can filter for these instead.
                    //AppInstance instance = instances.Where((i) => i.Key.StartsWith("REUSABLE")).FirstOrDefault();
                    //if (instance != null)
                    //{
                    //    Debug.WriteLine($"instance = {instance.Key}");
                    //    instance.RedirectActivationTo();
                    //}
                    //else
                    //{
                    //    AppInstance.FindOrRegisterInstanceForKey("REUSABLE" + App.Id.ToString());
                    //    UpdateSharedInstanceNumber();
                    //    global::Windows.UI.Xaml.Application.Start((p) => new App());
                    //}
                    var instance = AppInstance.FindOrRegisterInstanceForKey(_lastId.ToString());
                    if (instance.IsCurrentInstance)
                    {
                        // If we successfully registered this instance, we can now just
                        // go ahead and do normal XAML initialization.
                        global::Windows.UI.Xaml.Application.Start((p) => new App(0));
                    }
                    else
                    {
                        // Some other instance has registered for this key, so we'll 
                        // redirect this activation to that instance instead.
                        instance.RedirectActivationTo();
                    }
                }
            }
        }

        private static bool TryParseCommandLine(CommandLineActivatedEventArgs args, out int id, out bool test)
        {
#if !DEBUG
            if (args.PreviousExecutionState != ApplicationExecutionState.Terminated)
            {
                id = 0;
                test = false;
                return false;
            }
#endif

            try
            {
                var v_id = 0;
                var v_test = false;

                var p = new OptionSet()
                {
                    { "i|id=", (int v) => v_id = v },
                    { "t|test", v => v_test = v != null },
                };

                var extra = p.Parse(args.Operation.Arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

                id = v_id;
                test = v_test;
                return true;
            }
            catch
            {
                id = 0;
                test = false;
                return false;
            }
        }
    }
}
#endif
