/*
Copyright (c) 2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Matthias Freund

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;
using System.Linq;
using System.Windows.Controls;
using AasCore.Aas3_0;
using AasxIntegrationBase;
using Extensions;
using AdminShellNS;
using System.Threading.Tasks;
using AnyUi;
using AasxPluginAml;
using static AasxPluginAml.Utils.AmlSMUtils;
using static AasxPluginAml.Utils.BasicAasUtils;
using static AasxPluginAml.Utils.BasicAmlUtils;
using System.Windows.Forms;
using AasxPluginAml.Views;
using Aml.Engine.CAEX;
using AasxPluginAml.Utils;
using AasxPackageLogic;
using Aml.Engine.Adapter;
using NPOI.HPSF;
using System.IO;
using NPOI.SS.Formula.Functions;
using Aml.Engine.CAEX.Extensions;
using AasxPluginAml.AnyUi;

namespace AasxIntegrationBase // the namespace has to be: AasxIntegrationBase
{
    [UsedImplicitlyAttribute]
    // the class names has to be: AasxPlugin and subclassing IAasxPluginInterface
    public class AasxPlugin : AasxPluginBase
    {
        private AmlOptions _options = new AmlOptions();
        private AmlTreeView treeView = new AmlTreeView();

        public new void InitPlugin(string[] args)
        {
            // start ..
            PluginName = "AasxPluginAml";
            _log.Info("InitPlugin() called with args = {0}", (args == null) ? "" : string.Join(", ", args));
            
            // .. with built-in options
            _options = AmlOptions.CreateDefault();

            // try load defaults options from assy directory
            try
            {
                var newOpt =
                    AasxPluginOptionsBase.LoadDefaultOptionsFromAssemblyDir<AmlOptions>(
                        this.GetPluginName(), Assembly.GetExecutingAssembly());
                if (newOpt != null)
                    this._options = newOpt;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Exception when reading default options {1}");
            }
        }

        public new AasxPluginActionDescriptionBase[] ListActions()
        {
            _log.Info("ListActions() called");
            var res = new List<AasxPluginActionDescriptionBase>();
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "call-check-visual-extension",
                    "When called with Referable, returns possibly visual extension for it."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "get-licenses", "Gets a description of used licenses."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "set-json-options", "Sets plugin-options according to provided JSON string."));
            res.Add(new AasxPluginActionDescriptionBase(
                "get-json-options", "Gets plugin-options as a JSON string."));
            res.Add(new AasxPluginActionDescriptionBase("get-licenses", "Reports about used licenses."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "get-events", "Pops and returns the earliest event from the event stack."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "get-menu-items", "Provides a list of menu items of the plugin to the caller."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "call-menu-item", "Caller activates a named menu item.", useAsync: true));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "get-check-visual-extension", "Returns true, if plug-ins checks for visual extension."));
            res.Add(
                new AasxPluginActionDescriptionBase(
                    "fill-panel-visual-extension",
                    "When called, fill given WPF panel with control for graph display."));
            return res.ToArray();
        }

        public new AasxPluginResultBase ActivateAction(string action, params object[] args)
        {
            if (action == "call-check-visual-extension")
            {
                // arguments
                if (args.Length < 1)
                    return null;

                // looking only for Submodels
                var sm = args[0] as Submodel;
                if (sm == null)
                    return null;

                // check for a record in options, that matches Submodel
                var isAmlSubmodel = sm.GetAmlFile() != null;

                if (!isAmlSubmodel)
                    return null;
                // ReSharper enable UnusedVariable

                // success prepare record
                var cve = new AasxPluginResultVisualExtension("AML", "AML Tree Viewer");

                // ok
                return cve;
            }

            if (action == "set-json-options" && args != null && args.Length >= 1 && args[0] is string)
            {
                var newOpt = Newtonsoft.Json.JsonConvert.DeserializeObject<AmlOptions>(
                    (args[0] as string));
                if (newOpt != null)
                    this._options = newOpt;
            }

            if (action == "get-json-options")
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    this._options, Newtonsoft.Json.Formatting.Indented);
                return new AasxPluginResultBaseObject("OK", json);
            }

            if (action == "get-licenses")
            {
                var lic = new AasxPluginResultLicense();
                /*lic.shortLicense = "The OpenXML SDK is under MIT license." + Environment.NewLine +
                    "The ClosedXML library is under MIT license." + Environment.NewLine +
                    "The ExcelNumberFormat number parser is licensed under the MIT license." + Environment.NewLine +
                    "The FastMember reflection access is licensed under Apache License 2.0 (Apache - 2.0).";*/
                lic.shortLicense = "";

                lic.isStandardLicense = true;
                lic.longLicense = AasxPluginHelper.LoadLicenseTxtFromAssemblyDir(
                    "LICENSE.txt", Assembly.GetExecutingAssembly());

                return lic;
            }

            if (action == "get-events" && _eventStack != null)
            {
                // try access
                return _eventStack.PopEvent();
            }

            if (action == "get-menu-items")
            {
                // result list 
                var res = new List<AasxPluginResultSingleMenuItem>();

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "ImportAMLFile",
                        Header = "AutomationML: Import an AML file",
                        HelpText = "Import an AML file and create the relevant AAS elements",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "PublishAMLAttribute",
                        Header = "AutomationML: Publish AML attribute as AAS property",
                        HelpText = "Publish an AML attribute as an AAS property that is linked to the attribute",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "PublishAMLElement",
                        Header = "AutomationML: Publish AML element as AAS reference",
                        HelpText = "Publish an AML element (SUC/IE) as an AAS reference that is linked to the element",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "PublishAMLStructureExisting",
                        Header = "AutomationML: Publish AML element as AAS relationship to an existing BOM entity",
                        HelpText = "Publish an AML element (SUC/IE) as an AAS relationship that is linked to an existing entity within a BOM submodel",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "PublishAMLStructureNew",
                        Header = "AutomationML: Publish AML element as AAS relationship to a new BOM entity",
                        HelpText = "Publish an AML element (SUC/IE) as an AAS relationship that is linked to a new entity within a BOM submodel",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "InitializeInterfaceConnectorsSM",
                        Header = "Diamond: Initialize an Interface_Connectors Submodel for the selected AAS",
                        HelpText = "Initialize an Interface_Connectors Submodel for the selected AAS",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                res.Add(new AasxPluginResultSingleMenuItem()
                {
                    AttachPoint = "Plugins",
                    MenuItem = new AasxMenuItem()
                    {
                        Name = "GenerateAndIncludeAMLFile",
                        Header = "Diamond: Generate and Include an AML file for the selected AAS",
                        HelpText = "Generate an AML files based on the interface connectors submodel and include it in the AAS",
                        ArgDefs = new AasxMenuListOfArgDefs()
                    }
                });

                // return
                return new AasxPluginResultProvideMenuItems()
                {
                    MenuItems = res
                };
            }

            if (action == "get-check-visual-extension")
            {
                var cve = new AasxPluginResultBaseObject();
                cve.strType = "True";
                cve.obj = true;
                return cve;
            }

            if (action == "fill-panel-visual-extension")
            {
                // arguments
                if (args == null || args.Length < 3)
                    return null;

                var env = args[0] as AdminShellPackageEnv;
                var submodel = args[1] as Submodel;
                var panel = args[2] as DockPanel;

                object resobj = this.treeView.FillWithWpfControls(env, submodel, panel);

                // give object back
                var res = new AasxPluginResultBaseObject();
                res.obj = resobj;
                return res;


            }

            // default
            return null;
        }

        /// <summary>
        /// Async variant of <c>ActivateAction</c>.
        /// Note: for some reason of type conversion, it has to return <c>Task<object></c>.
        /// </summary>
        public new async Task<object> ActivateActionAsync(string action, params object[] args)
        {
            if (action == "call-menu-item")
            {
                try
                {
                    await HandleMenuItemCalled(args);
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, "when executing plugin menu item " + args[0] as string);
                }
            }

            // default
            return null;
        }

        private async Task HandleMenuItemCalled(object[] args)
        {
            IEnumerable<AasxPluginResultEventBase> resultEvents = null;

            var cmd = args[0] as string;
            var ticket = args[1] as AasxMenuActionTicket;
            var displayContext = args[2] as AnyUiContextPlusDialogs;

            var selectedAmlObject = GetSelectedAmlObject(args);
            var associatedSubmodel = GetAssociatedSubmodel(args);

            if (cmd == "importamlfile")
            {
                if (ticket.Package == null || ticket.DereferencedMainDataObject is not ISubmodel)
                {
                    return;
                }

                var result = await displayContext.MenuSelectOpenFilenameAsync(
                    null,
                    null,
                    "Select AML(X) file to import ..",
                    "*.aml", "AML files (*.aml)|AMLX packages (*.amlx)|Alle Dateien (*.*)|*.*",
                    "AML Import");

                var fileName = result.OriginalFileName;

                if (fileName == null)
                {
                    return;
                }

                var amlFile = ImportAmlFile(ticket.DereferencedMainDataObject as ISubmodel, fileName, ticket.Package);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = amlFile.GetReference()
                    }
                };

            } else if (cmd == "publishamlattribute")
            {
                if (selectedAmlObject == null || associatedSubmodel == null || selectedAmlObject is not AttributeType)
                {
                    return;
                }

                var propertySmc = PublishAmlAttribute(selectedAmlObject as AttributeType, associatedSubmodel);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = propertySmc.GetReference()
                    }
                };
            } else if (cmd == "publishamlelement")
            {
                if (selectedAmlObject == null || associatedSubmodel == null || selectedAmlObject is not SystemUnitClassType)
                {
                    return;
                }

                var elementReference = PublishAmlElement(selectedAmlObject as SystemUnitClassType, associatedSubmodel);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = elementReference.GetReference()
                    }
                };
            }
            else if (cmd == "publishamlstructureexisting")
            {
                if (selectedAmlObject == null || associatedSubmodel == null || selectedAmlObject is not SystemUnitClassType)
                {
                    return;
                }

                var selectEntityDialogData = new AnyUiDialogueDataSelectAasEntity();
                displayContext.StartFlyoverModal(selectEntityDialogData);

                if (!selectEntityDialogData.Result || selectEntityDialogData.ResultKeys.LastOrDefault()?.Type != KeyTypes.Entity)
                {
                    return;
                }

                var elementReference = PublishAmlStructureExisting(selectedAmlObject as SystemUnitClassType, associatedSubmodel, selectEntityDialogData.ResultKeys);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = elementReference.GetReference()
                    }
                };
            }
            else if (cmd == "publishamlstructurenew")
            {
                if (selectedAmlObject == null || associatedSubmodel == null || selectedAmlObject is not SystemUnitClassType)
                {
                    return;
                }

                var selectEntityDialogData = new AnyUiDialogueDataSelectAasEntity();
                displayContext.StartFlyoverModal(selectEntityDialogData);

                if (!selectEntityDialogData.Result)
                {
                    return;
                }

                var parentType = selectEntityDialogData.ResultKeys.LastOrDefault()?.Type;
                AasCore.Aas3_0.Environment env;

                if (parentType == KeyTypes.Entity)
                {
                    // we cannot simply use 'ticket.Env' as this is 'null' when the context is not a submodel(element) but our AML tree view
                    env = (selectEntityDialogData.ResultVisualElement as VisualElementSubmodelElement)?.theEnv;
                } else if (parentType == KeyTypes.Submodel)
                {
                    // we cannot simply use 'ticket.Env' as this is 'null' when the context is not a submodel(element) but our AML tree view
                    env = (selectEntityDialogData.ResultVisualElement as VisualElementSubmodelRef)?.theEnv;
                } else
                {
                    return;
                }

                if (env == null)
                {
                    return;
                }

                var parent = env.FindReferableByReference(new Reference(ReferenceTypes.ModelReference, selectEntityDialogData.ResultKeys)) as IReferable;

                if (parent == null)
                {
                    return;
                }

                var elementReference = PublishAmlStructureNew(selectedAmlObject as SystemUnitClassType, associatedSubmodel, parent);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = elementReference.GetReference()
                    }
                };
            }
            else if (cmd == "initializeinterfaceconnectorssm")
            {
                if (ticket.Package == null || ticket.DereferencedMainDataObject is not IAssetAdministrationShell)
                {
                    _log.Error($"No AAS was selcted...");
                    return;
                }

                var aas = ticket.DereferencedMainDataObject as IAssetAdministrationShell;

                var result = await InitInterfaceConnectorsSmDialog.DetermineInitInterfaceConnectorsSmConfiguration(displayContext);

                if (result == null)
                {
                    return;
                }

                var sm = CreateSubmodel("Interface_Connectors", _options.GetTemplateIdSubmodel(aas.GetSubjectId()), null, aas, ticket.Env);

                var connectors = new SubmodelElementCollection(idShort: "Connectors");
                sm.AddChild(connectors);

                var pneumaticProperties = new List<(string, string)>()
                {
                    ( "Designation", null ),
                    ( "ConnectorType", "Pneumatic" ),
                    ( "LogicalFunction", null ),
                    ( "DesignType", null )
                };

                for (var i = 0; i < result.NumberOfPneumaticConnectors; i++)
                {
                    var connector = new SubmodelElementCollection(idShort: $"PneumaticConnector{(i+1):00}");
                    connectors.AddChild(connector);

                    pneumaticProperties.ForEach(p =>
                    {
                        var prop = new AasCore.Aas3_0.Property(DataTypeDefXsd.String, idShort: p.Item1, value: p.Item2);
                        connector.AddChild(prop);
                    });
                }

                var electricProperties = new List<(string, string)>()
                {
                    ( "Designation", null ),
                    ( "ConnectorType", "Electric" ),
                    ( "LogicalFunction", null ),
                    ( "NumberOfPins", null ),
                    ( "Coding", null ),
                    ( "RatedVoltage", null ),
                    ( "RatedCurrent", null ),
                    ( "DesignType", null ),
                    ( "PlugType", null ),

                };

                for (var i = 0; i < result.NumberOfElectricConnectors; i++)
                {
                    var connector = new SubmodelElementCollection(idShort: $"ElectricConnector{(i + 1):00}");
                    connectors.AddChild(connector);

                    electricProperties.ForEach(p =>
                    {
                        var prop = new AasCore.Aas3_0.Property(DataTypeDefXsd.String, idShort: p.Item1, value: p.Item2);
                        connector.AddChild(prop);
                    });
                }

                var mechanicProperties = new List<(string, string)>()
                {
                    ( "Designation", null ),
                    ( "ConnectorType", "Mechanic" ),
                    ( "DesignType", null )
                };

                for (var i = 0; i < result.NumberOfMechanicConnectors; i++)
                {
                    var connector = new SubmodelElementCollection(idShort: $"MechanicConnector{(i + 1):00}");
                    connectors.AddChild(connector);

                    mechanicProperties.ForEach(p =>
                    {
                        var prop = new AasCore.Aas3_0.Property(DataTypeDefXsd.String, idShort: p.Item1, value: p.Item2);
                        connector.AddChild(prop);
                    });
                }

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = sm.GetReference()
                    }
                };
            }
            else if (cmd == "generateandincludeamlfile")
            {
                if (ticket.Package == null || ticket.DereferencedMainDataObject is not IAssetAdministrationShell)
                {
                    _log.Error($"No AAS was selcted...");
                    return;
                }

                var aas = ticket.DereferencedMainDataObject as IAssetAdministrationShell;

                // Find the 'Interface_Connectors' submodel that is the base for the generated AML
                var ifConSubmodel = FindAllSubmodels(ticket.Env, aas).FirstOrDefault(sm => sm.IdShort == "Interface_Connectors");

                if (ifConSubmodel == null)
                {
                    _log.Error($"Unable to find 'Interface_Connectors' submodel...");
                    return;
                }

                var connectorsSMC = ifConSubmodel?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme is ISubmodelElementCollection && sme.IdShort == "Connectors") as ISubmodelElementCollection;

                if (connectorsSMC == null || !connectorsSMC.Value.Any())
                {
                    _log.Error($"Unable to find 'Connectors' SMC within the 'Interface_Connectors' submodel...");
                    return;
                }

                var connectors = connectorsSMC.Value.Select(sme => sme as ISubmodelElementCollection).Where(smc => smc != null);

                var nameplateSM = FindAllSubmodels(ticket.Env, aas).FirstOrDefault(sm => sm.IdShort == "Nameplate");

                var manufacturerName = nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.GlobalReference, "0173-1#02-AAO677#002"))?.ValueAsText() ??
                    nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.ConceptDescription, "0173-1#02-AAO677#002"))?.ValueAsText();

                var articleNumber = nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.GlobalReference, "0173-1#02-AAO676#003"))?.ValueAsText() ??
                    nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.ConceptDescription, "0173-1#02-AAO676#003"))?.ValueAsText();

                var orderCode = nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.GlobalReference, "0173-1#02-AAO227#002"))?.ValueAsText() ??
                    nameplateSM?.OverSubmodelElementsOrEmpty().FirstOrDefault(sme => sme.HasSemanticId(KeyTypes.ConceptDescription, "0173-1#02-AAO227#002"))?.ValueAsText();

                var result = await GenerateAmlDialog.DetermineCreateAmlConfiguration(displayContext, $"{manufacturerName}_{articleNumber}", $"{articleNumber}_{orderCode}");

                if (result == null)
                {
                    return;
                }

                // Create the AML file
                var amlDocument = InitializeAmlDocumentFromTemplate();
                var amlFile = amlDocument.CAEXFile;

                var sucLib = amlFile.SystemUnitClassLib["Festo"];
                sucLib.Name = result.SucLibName;

                var suc = sucLib.SystemUnitClass["AutomationComponent"];
                suc.Name = result.SucName;
                suc.Attribute["globalAssetID"].Value = aas.AssetInformation.GlobalAssetId;

                var connectorCollection = suc.InternalElement["ConnectorCollection"];

                foreach (var connector in connectors)
                {
                    var connectorName = connector.IdShort;
                    var connectorType = connector.OverValueOrEmpty().FirstOrDefault(sme => sme.IdShort == "ConnectorType")?.ValueAsText();

                    var connectorIE = connectorCollection.InternalElement.Append(connectorName);
                    try
                    {
                        AddRoleToElement(connectorIE, $"AutomationML_Component_RoleClassLib_ConnectorExtension/{connectorType}Connector");
                    } catch(Exception ex)
                    {
                        _log.Error(ex.Message, ex);
                    }

                    AddExternalInterfaceToElement(connectorIE, "DIAMONDInterfaceClassLib/AASXInternalConnector");
                    var connectorReference = String.Join("/", connector.GetReference().Keys.Select(key => key.Value));
                    connectorIE.ExternalInterface["AASXInternalConnector"].Attribute["AAS_Ref"].Value = connectorReference;
                }

                var ecadSM = CreateSubmodel(result.SubmodelName, _options.GetTemplateIdSubmodel(aas.GetSubjectId()), null, aas, ticket.Env);

                string amlTempFilePath = Path.Combine(Path.GetTempPath(), $"{articleNumber}.aml");
                amlDocument.SaveToFile(amlTempFilePath, true);

                ImportAmlFile(ecadSM, amlTempFilePath, ticket.Package);

                resultEvents = new List<AasxPluginResultEventBase>() {
                    new AasxPluginResultEventRedrawAllElements(),
                    new AasxPluginResultEventNavigateToReference()
                    {
                        targetReference = ecadSM.GetReference()
                    }
                };
            }

            resultEvents?.ToList().ForEach(r => _eventStack.PushEvent(r));
        }

        private void AddRoleToElement(SystemUnitClassType element, string referencedRoleClassPath)
        {
            if (element.CAEXDocument.FindByPath(referencedRoleClassPath) == null)
            {
                throw new Exception($"Unable to find RoleClass with path '{referencedRoleClassPath}'");
            }

            var supportedRoleClass = element.New_SupportedRoleClass(referencedRoleClassPath);
            var role = supportedRoleClass.RoleClass;

            foreach (var attribute in role.Attribute)
            {
                element.Attribute.Insert(attribute.Copy() as AttributeType, false);
            }

            foreach( var externalInterface in role.ExternalInterface)
            {
                element.ExternalInterface.Insert(externalInterface.Copy() as ExternalInterfaceType, false);
            }
        }

        private void AddExternalInterfaceToElement(SystemUnitClassType element, string referencedInterfaceClassPath)
        {
            if (element.CAEXDocument.FindByPath(referencedInterfaceClassPath) == null)
            {
                throw new Exception($"Unable to find InterfaceClass with path '{referencedInterfaceClassPath}'");
            }

            var interfaceName = referencedInterfaceClassPath.Split("/").Last();
            var externalInterface = element.New_ExternalInterface(interfaceName, referencedInterfaceClassPath);

            foreach (var attribute in (element.CAEXDocument.FindByPath(referencedInterfaceClassPath) as InterfaceClassType).Attribute)
            {
                externalInterface.Attribute.Insert(attribute.Copy() as AttributeType, false);
            }
        }

        private CAEXObject? GetSelectedAmlObject(params object[] args)
        {
            var amlViewerPanel = FindAmlViewerPanel(args[3] as DockPanel);

            return amlViewerPanel?.SelectedObject;
        }

        private Submodel? GetAssociatedSubmodel(params object[] args)
        {
            var amlViewerPanel = FindAmlViewerPanel(args[3] as DockPanel);

            return amlViewerPanel?.AssociatedSubmodel;
        }

        private AmlViewerPanel FindAmlViewerPanel(DockPanel parent)
        {
            if (parent == null)
            {
                return null;
            }

            foreach(var child in parent.Children)
            {
                if (child is AmlViewerPanel)
                {
                    return child as AmlViewerPanel;
                }
            }

            return null;
        }

        /// <summary>
        /// Load and parse the resource containing the AML template file to use for the generation.
        /// </summary>
        /// <returns>The parsed CAEXDocument</returns>
        protected static CAEXDocument InitializeAmlDocumentFromTemplate()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AasxPluginAml.Resources.template-caex-30.aml"))
            {
                return CAEXDocument.LoadFromStream(stream);
            };
        }
    }
}
