﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Xml.Linq;
using System.Xml.Serialization;
using AuthService;
using FenixHelper;
using FenixSoapWebService.FenixAppService;

namespace FenixSoapWebService
{
	/// <summary>
	/// Zpracování potvrzení/oznámení, které přišlo z ND
	/// - potvrzení recepce ReceptionConfirmation (message R1)
	/// - potvrzení kittingu KittingConfirmation (message K1)
	/// - potvrzení závozu/expedice ShipmentConfirmation (message S1)
	/// - vratky (vrácené zařízení - message VR1)
	/// - vratky (vrácené itemy - message VR2)
	/// - vratky (závoz na repasi CPE - message VR3)
	/// - repase - potvrzení naskladnění repasovaného zboží RefurbishedConfirmation (message RF1)
	/// </summary>
	public class MessageProcessing
	{
		/// <summary>
		/// Vlastní zpracování XML message
		/// (zrušení deklarační části, zušení všech jmenných prostorů a volání stored procedure na MS SQL)
		/// </summary>
		/// <param name="login"></param>
		/// <param name="password"></param>
		/// <param name="partnerCode"></param>
		/// <param name="messageType"></param>
		/// <param name="xmlString"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static SubmitDataToProcessingResult Process(AuthService.AuthToken authToken, string login, string password, string partnerCode, string messageType, byte[] data, string encoding)
		{
			SubmitDataToProcessingResult result = new SubmitDataToProcessingResult();
			string xmlString = String.Empty;

			try
			{	
				if (authToken != null)
				{
					xmlString = data.ToString(Encoding.GetEncoding(encoding), Encoding.Unicode);
					xmlString = prepareXml(xmlString);

					result = ProjectHelper.AppLogWrite(authToken, "XML",
													   ProjectHelper.CreateAppLogMessage(partnerCode, messageType, "modified XML"), "", 
													   xmlString, BC.ZICYZ_USER_ID, AppLog.GetMethodName());

					if (result.MessageNumber == BC.OK)
					{
						FenixAppSvcClient appClient = new FenixAppSvcClient();
						appClient.AuthToken = new FenixAppService.AuthToken() { Value = authToken.Value };
						ProcResult procResult = doProcess(appClient, xmlString, messageType);
						appClient.Close();
												
						if (procResult.ReturnValue == (int)BC.OK)
						{
							ProjectHelper.CreateOKResult(ref result);
						}
						else
						{
							ProjectHelper.CreateErrorResult(FenixHelper.AppLog.GetMethodName(), ref result, "90", procResult.ReturnMessage);
						}
					}					
				}
			}
			catch (Exception ex)
			{
				ProjectHelper.CreateErrorResult(FenixHelper.AppLog.GetMethodName(), ref result, "100", ex);				
				ProjectHelper.AppLogWrite(authToken, "ERROR", 
					                      String.Format("Během zpracování XML\n{0}\ndošlo k chybě\n{1}", xmlString, ex.Message), 
										  String.Empty, String.Empty, BC.ZICYZ_USER_ID, AppLog.GetMethodName());				
			}

			return result;
		}

		private static ProcResult doProcess(FenixAppSvcClient appClient, string xmlString, string messageType)
		{
			ProcResult procResult = new ProcResult();
			
			string messageTypeNormalized = messageType.ToUpper().Trim();

			switch (messageTypeNormalized)
			{
				case "RECEPTIONCONFIRMATION":
					procResult = appClient.ReceptionConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "KITTINGCONFIRMATION":					
					procResult = appClient.KittingConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "SHIPMENTCONFIRMATION":					
					procResult = appClient.ShipmentConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "RETURNED":
				case "RETURNEDEQUIPMENT":
					procResult = appClient.ReturnedEquipmentProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "RETURNEDITEM":
					procResult = appClient.ReturnedItemProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "RETURNEDSHIPMENT":
					procResult = appClient.ReturnedShipmentProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "REFURBISHEDCONFIRMATION":
					procResult = appClient.RefurbishedConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "DELETEMESSAGECONFIRMATION":
					procResult = appClient.DeleteMessageConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "CRMORDERCONFIRMATION":
					procResult = appClient.CrmOrderConfirmationProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				case "CRMORDERAPPROVAL":
					procResult = appClient.CrmOrderApprovalProcess(xmlString, BC.ZICYZ_USER_ID);
					break;

				default:
					procResult.ReturnValue = (int)BC.NOT_OK;
					procResult.ReturnMessage = String.Format("Unknown messageType = [{0}]", messageType);					
					break;
			}

			return procResult;
		}
		
		/// <summary>
		/// Příprava XML stringu pro zápis do MS SQL
		/// (vytvoření root elementu, zrušení všech jmenných prostorů)
		/// </summary>
		/// <param name="xmlString"></param>
		/// <returns></returns>
		private static string prepareXml(string xmlString)
		{	
			string modifiedXmlString = XmlCreator.CreateXMLRootNode(xmlString);
			return modifiedXmlString;
		}
	}
}