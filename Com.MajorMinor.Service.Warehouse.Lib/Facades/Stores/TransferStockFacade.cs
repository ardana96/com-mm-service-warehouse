﻿using Com.MM.Service.Warehouse.Lib.Helpers;
using Com.MM.Service.Warehouse.Lib.Interfaces;
using Com.MM.Service.Warehouse.Lib.Models.Expeditions;
using Com.MM.Service.Warehouse.Lib.Models.InventoryModel;
using Com.MM.Service.Warehouse.Lib.Models.SPKDocsModel;
using Com.MM.Service.Warehouse.Lib.Models.TransferModel;
using Com.MM.Service.Warehouse.Lib.ViewModels.NewIntegrationViewModel;
using Com.MM.Service.Warehouse.Lib.ViewModels.TransferViewModels;
using Com.Moonlay.Models;
using Com.Moonlay.NetCore.Lib;
using HashidsNet;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.MM.Service.Warehouse.Lib.Facades.Stores
{
    public class TransferStockFacade
    {
        private string USER_AGENT = "Facade";

        private readonly WarehouseDbContext dbContext;
        private readonly DbSet<TransferInDoc> dbSetTransferIn;
        private readonly DbSet<SPKDocs> dbSetSpk;
        private readonly IServiceProvider serviceProvider;
        private readonly DbSet<Inventory> dbSetInventory;
        private readonly DbSet<InventoryMovement> dbSetInventoryMovement;
        private readonly DbSet<Expedition> dbSetExpedition;
        private readonly DbSet<TransferOutDoc> dbSet;

        public TransferStockFacade(IServiceProvider serviceProvider, WarehouseDbContext dbContext)
        {
            this.serviceProvider = serviceProvider;
            this.dbContext = dbContext;
            this.dbSetTransferIn = dbContext.Set<TransferInDoc>();
            this.dbSetInventory = dbContext.Set<Inventory>();
            this.dbSetSpk = dbContext.Set<SPKDocs>();
            this.dbSetInventoryMovement = dbContext.Set<InventoryMovement>();
            this.dbSetExpedition = dbContext.Set<Expedition>();
            this.dbSet = dbContext.Set<TransferOutDoc>();
        }
        public string GenerateCode(string ModuleId)
        {
            var uid = ObjectId.GenerateNewId().ToString();
            var hashids = new Hashids(uid, 8, "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890");
            var now = DateTime.Now;
            var begin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var diff = (now - begin).Milliseconds;
            string code = String.Format("{0}/{1}/{2}", hashids.Encode(diff), ModuleId, DateTime.Now.ToString("MM/yyyy"));
            return code;
        }
        public async Task<int> Create(TransferOutDocViewModel model, TransferOutDoc model2, string username, int clientTimeZoneOffset = 7)
        {
            int Created = 0;

            using (var transaction = this.dbContext.Database.BeginTransaction())
            {
                try
                {
                    string codeOut = GenerateCode("MM-KB/RTT");
                    string packingList1 = GenerateCode("MM-KB/PLR");
                    string CodeIn = GenerateCode("MM-TB/BRT");
                    string packingList2 = GenerateCode("MM-KB/PLB");
                    string expCode = GenerateCode("MM-KB/EXP");
                    string codetransferin = GenerateCode("MM-TB/BRT");
                    model2.Code = codeOut;
                    model2.Date = DateTimeOffset.Now;
                    var storages = GetStorage("GTM.01");
                    var expeditionService = GetExpedition("Dikirim Sendiri");
                    List<ExpeditionItem> expeditionItems = new List<ExpeditionItem>();
                    List<ExpeditionDetail> expeditionDetails = new List<ExpeditionDetail>();
                    List<SPKDocsItem> sPKDocsItem1 = new List<SPKDocsItem>();
                    List<SPKDocsItem> sPKDocsItem2 = new List<SPKDocsItem>();
                    List<TransferInDocItem> transferInDocs = new List<TransferInDocItem>();
                    List<InventoryMovement> inventoryMovements = new List<InventoryMovement>();
                    List<TransferOutDocItem> transferOutDocItems = new List<TransferOutDocItem>();
                    EntityExtension.FlagForCreate(model2, username, USER_AGENT);
                    foreach (var i in model2.Items)
                    {
                        var invenInTransfer = dbContext.Inventories.Where(x => x.ItemId == i.ItemId && x.StorageId == storages.Id).FirstOrDefault();
                        if (invenInTransfer == null)
                        {
                            Inventory inventory = new Inventory
                            {
                                ItemArticleRealizationOrder = i.ArticleRealizationOrder,
                                ItemCode = i.ItemCode,
                                ItemDomesticCOGS = i.DomesticCOGS,
                                ItemDomesticRetail = i.DomesticRetail,
                                ItemDomesticSale = i.DomesticSale,
                                ItemDomesticWholeSale = i.DomesticWholeSale,
                                ItemId = i.ItemId,
                                ItemInternationalCOGS = 0,
                                ItemInternationalRetail = 0,
                                ItemInternationalSale = 0,
                                ItemInternationalWholeSale = 0,
                                ItemName = i.ItemName,
                                ItemSize = i.Size,
                                ItemUom = i.Uom,
                                Quantity = 0,
                                StorageCode = storages.Code,
                                StorageId = storages.Id,
                                StorageName = storages.Name,
                                StorageIsCentral = storages.Name.Contains("GUDANG") ? true : false,
                            };
                            EntityExtension.FlagForCreate(inventory, username, USER_AGENT);
                            dbSetInventory.Add(inventory);
                            transferOutDocItems.Add(new TransferOutDocItem
                            {
                                ArticleRealizationOrder = i.ArticleRealizationOrder,
                                DomesticCOGS = i.DomesticCOGS,
                                DomesticRetail = i.DomesticRetail,
                                DomesticSale = i.DomesticSale,
                                DomesticWholeSale = i.DomesticWholeSale,
                                ItemCode = i.ItemCode,
                                ItemId = i.ItemId,
                                ItemName = i.ItemName,
                                Quantity = i.Quantity,
                                Remark = i.Remark,
                                Size = i.Size,
                                Uom = i.Uom
                            });
                        }
                        else
                        {
                            invenInTransfer.Quantity = invenInTransfer.Quantity - i.Quantity;
                        }
                        sPKDocsItem1.Add(new SPKDocsItem
                        {
                            ItemArticleRealizationOrder = i.ArticleRealizationOrder,
                            ItemCode = i.ItemCode,
                            ItemDomesticCOGS = i.DomesticCOGS,
                            ItemDomesticRetail = i.DomesticRetail,
                            ItemDomesticSale = i.DomesticSale,
                            ItemDomesticWholesale = i.DomesticWholeSale,
                            ItemId = i.ItemId,
                            ItemName = i.ItemName,
                            ItemSize = i.Size,
                            ItemUom = i.Uom,
                            Quantity = i.Quantity,
                            Remark = i.Remark,
                            SendQuantity = i.Quantity
                        });
                        sPKDocsItem2.Add(new SPKDocsItem
                        {
                            ItemArticleRealizationOrder = i.ArticleRealizationOrder,
                            ItemCode = i.ItemCode,
                            ItemDomesticCOGS = i.DomesticCOGS,
                            ItemDomesticRetail = i.DomesticRetail,
                            ItemDomesticSale = i.DomesticSale,
                            ItemDomesticWholesale = i.DomesticWholeSale,
                            ItemId = i.ItemId,
                            ItemName = i.ItemName,
                            ItemSize = i.Size,
                            ItemUom = i.Uom,
                            Quantity = i.Quantity,
                            Remark = i.Remark,
                            SendQuantity = i.Quantity
                        });
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                    }
                    EntityExtension.FlagForCreate(model2, username, USER_AGENT);
                    foreach (var i in model2.Items)
                    {
                        //var inventorymovement = new InventoryMovement();
                        
                        transferInDocs.Add(new TransferInDocItem
                        {
                            ArticleRealizationOrder = i.ArticleRealizationOrder,
                            ItemCode = i.ItemCode,
                            DomesticCOGS = i.DomesticCOGS,
                            DomesticRetail = i.DomesticRetail,
                            DomesticSale = i.DomesticSale,
                            DomesticWholeSale = i.DomesticWholeSale,
                            ItemId = i.ItemId,
                            ItemName = i.ItemName,
                            Size = i.Size,
                            Uom = i.Uom,
                            Quantity = i.Quantity,
                            Remark = i.Remark
                        });
                    }
                    SPKDocs sPKDocs1 = new SPKDocs
                    {
                        Code = GenerateCode("MM-PK/PBJ"),
                        Date = DateTimeOffset.Now,
                        SourceId = model2.SourceId,
                        SourceCode = model2.SourceCode,
                        SourceName = model2.SourceName,
                        DestinationId = storages.Id,
                        DestinationCode = storages.Code,
                        DestinationName = storages.Name,
                        IsDistributed = true,
                        IsReceived = true,
                        IsDraft = false,
                        PackingList = packingList1,
                        Reference = codeOut,
                        Password = String.Join("", GenerateCode(DateTime.Now.ToString("dd")).Split("/")),
                        Weight = 0,
                        Items = sPKDocsItem1
                    };
                    EntityExtension.FlagForCreate(sPKDocs1, username, USER_AGENT);

                    TransferInDoc transferInDoc = new TransferInDoc
                    {
                        Code = codetransferin,
                        Date = DateTimeOffset.Now,
                        DestinationId = storages.Id,
                        DestinationCode = storages.Code,
                        DestinationName = storages.Name,
                        SourceCode = model2.SourceCode,
                        SourceId = model2.SourceId,
                        SourceName = model2.SourceName,
                        Reference = packingList1,
                        Remark = "",
                        Items = transferInDocs
                    };
                    EntityExtension.FlagForCreate(transferInDoc, username, USER_AGENT);

                    SPKDocs sPKDocs2 = new SPKDocs
                    {
                        Code = GenerateCode("MM-PK/PBJ"),
                        Date = DateTimeOffset.Now,
                        DestinationId = model2.DestinationId,
                        DestinationCode = model2.DestinationCode,
                        DestinationName = model2.DestinationName,
                        SourceId = storages.Id,
                        SourceCode = storages.Code,
                        SourceName = storages.Name,
                        IsDistributed = true,
                        IsReceived = false,
                        IsDraft = false,
                        PackingList = packingList2,
                        Reference = codeOut,
                        Password = String.Join("", GenerateCode(DateTime.Now.ToString("dd")).Split("/")),
                        Weight = 0,
                        Items = sPKDocsItem2
                    };
                    EntityExtension.FlagForCreate(sPKDocs2, username, USER_AGENT);

                    foreach (var i in sPKDocs1.Items)
                    {
                        var QtySource = 0.0;
                        var invenOutSource = dbContext.Inventories.Where(x => x.ItemId == i.ItemId && x.StorageId == model2.SourceId).FirstOrDefault();
                        
                        if (invenOutSource != null)
                        {
                            QtySource = invenOutSource.Quantity;
                            invenOutSource.Quantity = invenOutSource.Quantity - i.Quantity;
                        }

                        inventoryMovements.Add(new InventoryMovement
                        {
                            Before = QtySource,
                            After = invenOutSource.Quantity,
                            Date = DateTimeOffset.Now,
                            ItemCode = i.ItemCode,
                            ItemDomesticCOGS = i.ItemDomesticCOGS,
                            ItemDomesticRetail = i.ItemDomesticRetail,
                            ItemDomesticWholeSale = i.ItemDomesticRetail,
                            ItemDomesticSale = i.ItemDomesticSale,
                            ItemId = i.ItemId,
                            ItemInternationalCOGS = 0,
                            ItemInternationalRetail = 0,
                            ItemInternationalSale = 0,
                            ItemInternationalWholeSale = 0,
                            ItemName = i.ItemName,
                            ItemSize = i.ItemSize,
                            ItemUom = i.ItemUom,
                            Quantity = i.Quantity,
                            StorageCode = model2.SourceCode,
                            StorageId = model2.SourceId,
                            StorageName = model2.SourceName,
                            Type = "OUT",
                            Reference = codeOut,
                            Remark = model2.Remark,
                            StorageIsCentral = model2.SourceName.Contains("GUDANG") ? true : false,
                        });
                        inventoryMovements.Add(new InventoryMovement
                        {
                            Before = 0,
                            After = 1,
                            Date = DateTimeOffset.Now,
                            ItemCode = i.ItemCode,
                            ItemDomesticCOGS = i.ItemDomesticCOGS,
                            ItemDomesticRetail = i.ItemDomesticRetail,
                            ItemDomesticWholeSale = i.ItemDomesticRetail,
                            ItemDomesticSale = i.ItemDomesticSale,
                            ItemId = i.ItemId,
                            ItemInternationalCOGS = 0,
                            ItemInternationalRetail = 0,
                            ItemInternationalSale = 0,
                            ItemInternationalWholeSale = 0,
                            ItemName = i.ItemName,
                            ItemSize = i.ItemSize,
                            ItemUom = i.ItemUom,
                            Quantity = i.Quantity,
                            StorageCode = storages.Code,
                            StorageId = storages.Id,
                            StorageName = storages.Name,
                            Type = "IN",
                            Reference = codetransferin,
                            Remark = model2.Remark,
                            StorageIsCentral = storages.Name.Contains("GUDANG") ? true : false,
                        });
                        inventoryMovements.Add(new InventoryMovement
                        {
                            Before = 1,
                            After = 0,
                            Date = DateTimeOffset.Now,
                            ItemCode = i.ItemCode,
                            ItemDomesticCOGS = i.ItemDomesticCOGS,
                            ItemDomesticRetail = i.ItemDomesticRetail,
                            ItemDomesticWholeSale = i.ItemDomesticRetail,
                            ItemDomesticSale = i.ItemDomesticSale,
                            ItemId = i.ItemId,
                            ItemInternationalCOGS = 0,
                            ItemInternationalRetail = 0,
                            ItemInternationalSale = 0,
                            ItemInternationalWholeSale = 0,
                            ItemName = i.ItemName,
                            ItemSize = i.ItemSize,
                            ItemUom = i.ItemUom,
                            Quantity = i.Quantity,
                            StorageCode = model2.DestinationCode,
                            StorageId = model2.DestinationId,
                            StorageName = model2.DestinationName,
                            Type = "OUT",
                            Reference = expCode,
                            Remark = model2.Remark,
                            StorageIsCentral = model2.DestinationName.Contains("GUDANG") ? true : false,
                        });
                        expeditionDetails.Add(new ExpeditionDetail
                        {
                            ArticleRealizationOrder = i.ItemArticleRealizationOrder,
                            DomesticCOGS = i.ItemDomesticCOGS,
                            DomesticRetail = i.ItemDomesticRetail,
                            DomesticSale = i.ItemDomesticSale,
                            DomesticWholesale = i.ItemDomesticWholesale,
                            ItemCode = i.ItemCode,
                            ItemId = i.ItemId,
                            ItemName = i.ItemName,
                            Quantity = i.Quantity,
                            Remark = i.Remark,
                            SendQuantity = i.SendQuantity,
                            Uom = i.ItemUom,
                            Size = i.ItemSize,
                            //SPKDocsId = (int)dbContext.SPKDocs.OrderByDescending(x=>x.Id).FirstOrDefault().Id + 1
                            SPKDocsId = (int)sPKDocs1.Id
                        });
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                    }
                    expeditionItems.Add(new ExpeditionItem
                    {
                        Code = sPKDocs2.Code,
                        Date = sPKDocs2.Date,
                        DestinationCode = sPKDocs2.DestinationCode,
                        DestinationId = (int)sPKDocs2.DestinationId,
                        DestinationName = sPKDocs2.DestinationName,
                        IsDistributed = sPKDocs2.IsDistributed,
                        IsDraft = sPKDocs2.IsDraft,
                        IsReceived = sPKDocs2.IsReceived,
                        PackingList = sPKDocs2.PackingList,
                        Password = sPKDocs2.Password,
                        Reference = codeOut,
                        SourceCode = sPKDocs2.SourceCode,
                        SourceId = (int)sPKDocs2.SourceId,
                        SourceName = sPKDocs2.SourceName,
                        //SPKDocsId = (int)dbContext.SPKDocs.OrderByDescending(x => x.Id).FirstOrDefault().Id + 1,
                        SPKDocsId = (int)sPKDocs2.Id,
                        Weight = sPKDocs2.Weight,
                        Details = expeditionDetails
                    });

                    Expedition expedition = new Expedition
                    {
                        Code = expCode,
                        Date = DateTimeOffset.Now,
                        ExpeditionServiceCode = expeditionService.code,
                        ExpeditionServiceId = (int)expeditionService._id,
                        ExpeditionServiceName = expeditionService.name,
                        Remark = "",
                        Weight = 0,
                        Items = expeditionItems,

                    };
                    EntityExtension.FlagForCreate(expedition, username, USER_AGENT);

                    TransferOutDoc transferOutDoc2 = new TransferOutDoc
                    {
                        Code = expCode,
                        Date = DateTimeOffset.Now,
                        DestinationCode = model2.DestinationCode,
                        DestinationId = model2.DestinationId,
                        DestinationName = model2.DestinationName,
                        Reference = packingList2,
                        Remark = "",
                        SourceId = storages.Id,
                        SourceCode = storages.Code,
                        SourceName = storages.Name,
                        Items = transferOutDocItems
                    };
                    EntityExtension.FlagForCreate(transferOutDoc2, username, USER_AGENT);
                    #region Saving
                    foreach(var i in transferOutDoc2.Items)
                    {
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                    }
                    foreach (var i in sPKDocs2.Items)
                    {
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                    }
                    foreach (var i in expedition.Items)
                    {
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                        foreach(var d in i.Details)
                        {
                            EntityExtension.FlagForCreate(d, username, USER_AGENT);
                        }
                    }
                    foreach(var i in transferInDoc.Items)
                    {
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                    }
                    foreach(var i in inventoryMovements)
                    {
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                        dbSetInventoryMovement.Add(i);
                    }
                    dbSetExpedition.Add(expedition);
                    dbSetSpk.Add(sPKDocs1);
                    dbSetSpk.Add(sPKDocs2);
                    dbSet.Add(model2);
                    dbSet.Add(transferOutDoc2);
                    dbSetTransferIn.Add(transferInDoc);

                    Created = await dbContext.SaveChangesAsync();
                    transaction.Commit();

                    #endregion

                }
                catch(Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }

                return Created;
            }
        }
        private StorageViewModel2 GetStorage(string code)
        {
            string itemUri = "storages/code";
            string queryUri = "?code=" + code;
            string uri = itemUri + queryUri;
            IHttpClientService httpClient = (IHttpClientService)serviceProvider.GetService(typeof(IHttpClientService));
            var response = httpClient.GetAsync($"{APIEndpoint.Core}{uri}").Result;
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().Result;
                Dictionary<string, object> result = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                StorageViewModel2 viewModel = JsonConvert.DeserializeObject<StorageViewModel2>(result.GetValueOrDefault("data").ToString());
                return viewModel;//.Where(x => x.dataDestination[0].name == name && x.dataDestination[0].code == code).FirstOrDefault();
                //throw new Exception(string.Format("{0}, {1}, {2}", response.StatusCode, response.Content, APIEndpoint.Purchasing));
            }
            else
            {
                return null;
            }
        }
        private ExpeditionServiceViewModel GetExpedition(string code)
        {
            string itemUri = "expedition-service-routers/all/code";
            string queryUri = "?code=" + code;
            string uri = itemUri + queryUri;
            IHttpClientService httpClient = (IHttpClientService)serviceProvider.GetService(typeof(IHttpClientService));
            var response = httpClient.GetAsync($"{APIEndpoint.Core}{uri}").Result;
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().Result;
                Dictionary<string, object> result = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                ExpeditionServiceViewModel viewModel = JsonConvert.DeserializeObject<ExpeditionServiceViewModel>(result.GetValueOrDefault("data").ToString());
                return viewModel;//.Where(x => x.dataDestination[0].name == name && x.dataDestination[0].code == code).FirstOrDefault();
                //throw new Exception(string.Format("{0}, {1}, {2}", response.StatusCode, response.Content, APIEndpoint.Purchasing));
            }
            else
            {
                return null;
            }
        }
        public Tuple<List<TransferOutDoc>, int, Dictionary<string, string>> Read(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            IQueryable<TransferOutDoc> Query = this.dbSet.Include(m => m.Items);

            List<string> searchAttributes = new List<string>()
            {
                "Code"
            };

            Query = QueryHelper<TransferOutDoc>.ConfigureSearch(Query, searchAttributes, Keyword);

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            Query = QueryHelper<TransferOutDoc>.ConfigureFilter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = QueryHelper<TransferOutDoc>.ConfigureOrder(Query, OrderDictionary);

            Pageable<TransferOutDoc> pageable = new Pageable<TransferOutDoc>(Query, Page - 1, Size);
            List<TransferOutDoc> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }

        public Tuple<List<TransferStockViewModel>, int, Dictionary<string, string>> ReadModel(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            var Query = from a in dbContext.TransferOutDocs
                       join b in dbContext.SPKDocs on a.Code equals b.Reference
                       where a.Code.Contains("MM-KB/RTT") && b.DestinationName != "GUDANG TRANSFER STOCK MAJOR MINOR"
                       select new TransferStockViewModel
                       {
                           id = (int)a.Id,
                           code = a.Code,
                           createdBy = a.CreatedBy,
                           createdDate = a.CreatedUtc,
                           destinationname = a.DestinationName,
                           destinationcode = a.DestinationCode,
                           sourcename = a.SourceName,
                           sourcecode = a.SourceCode,
                           password = b.Password,
                           referensi = a.Reference,
                           transfername = b.SourceName,
                           transfercode = b.SourceCode
                       };

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            //Query = QueryHelper<TransferOutDoc>.ConfigureOrder(Query, OrderDictionary);

            Pageable<TransferStockViewModel> pageable = new Pageable<TransferStockViewModel>(Query, Page - 1, Size);
            List<TransferStockViewModel> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }
        public TransferStockViewModel ReadById(int id)
        {
            var Query = from a in dbContext.TransferOutDocs
                        join b in dbContext.SPKDocs on a.Code equals b.Reference
                        join c in dbContext.TransferOutDocItems on a.Id equals c.TransferOutDocsId
                        where a.Code.Contains("MM-KB/RTT") && b.DestinationName != "GUDANG TRANSFER STOCK MAJOR MINOR"
                        && a.Id == id
                        select new 
                        {
                            id = (int)a.Id,
                            code = a.Code,
                            createdBy = a.CreatedBy,
                            createdDate = a.CreatedUtc,
                            destinationname = a.DestinationName,
                            sourcecode = a.SourceCode,
                            sourcename = a.SourceName,
                            password = b.Password,
                            transfercode = b.SourceCode,
                            destinationcode = a.DestinationCode,
                            referensi = a.Reference,
                            transfername = b.SourceName,
                            itemCode = c.ItemCode,
                            itemName = c.ItemName,
                            quantity = c.Quantity,
                            price = c.DomesticSale,
                            remark = c.Remark
                        };
            List<TransferOutDocItemViewModel> transferOutDocsitems = new List<TransferOutDocItemViewModel>();
            foreach(var i in Query)
            {
                transferOutDocsitems.Add(new TransferOutDocItemViewModel
                {
                    item = new ItemViewModel
                    {
                        code = i.itemCode,
                        name = i.itemName,
                        domesticSale = i.price,

                    },
                    quantity = i.quantity,
                    remark = i.remark
                });
            }

            TransferStockViewModel model = new TransferStockViewModel
            {
                code = Query.FirstOrDefault().code,
                createdBy = Query.FirstOrDefault().createdBy,
                createdDate = Query.FirstOrDefault().createdDate,
                destinationcode = Query.FirstOrDefault().destinationcode,
                destinationname = Query.FirstOrDefault().destinationname,
                id = Query.FirstOrDefault().id,
                password = Query.FirstOrDefault().password,
                referensi = Query.FirstOrDefault().referensi,
                sourcecode = Query.FirstOrDefault().sourcecode,
                sourcename = Query.FirstOrDefault().sourcename,
                transfercode = Query.FirstOrDefault().transfercode,
                transfername = Query.FirstOrDefault().transfername,
                items = transferOutDocsitems
            };
            //var model = dbSet.Where(m => m.Id == id)
            //     .Include(m => m.Items)
            //     .FirstOrDefault();
            return model;
        }
    }
}
