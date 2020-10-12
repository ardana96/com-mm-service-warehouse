﻿
using Com.MM.Service.Warehouse.Lib.Interfaces.TransferInterfaces;
using Com.MM.Service.Warehouse.Lib.Models.TransferModel;
using Com.Moonlay.NetCore.Lib;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Com.MM.Service.Warehouse.Lib.Helpers;
using System.Threading.Tasks;
using Com.Moonlay.Models;
using Com.MM.Service.Warehouse.Lib.Models.InventoryModel;
using HashidsNet;
using MongoDB.Bson;

namespace Com.MM.Service.Warehouse.Lib.Facades
{
    public class TransferFacade : ITransferInDoc
    {
        private string USER_AGENT = "Facade";

        private readonly WarehouseDbContext dbContext;
        private readonly DbSet<TransferInDoc> dbSet;
        private readonly IServiceProvider serviceProvider;
        private readonly DbSet<Inventory> dbSetInventory;
        private readonly DbSet<InventoryMovement> dbSetInventoryMovement;

        public TransferFacade(IServiceProvider serviceProvider, WarehouseDbContext dbContext)
        {
            this.serviceProvider = serviceProvider;
            this.dbContext = dbContext;
            this.dbSet = dbContext.Set<TransferInDoc>();
            this.dbSetInventory = dbContext.Set<Inventory>();
            this.dbSetInventoryMovement = dbContext.Set<InventoryMovement>();
        }

        public Tuple<List<TransferInDoc>, int, Dictionary<string, string>> Read(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            IQueryable<TransferInDoc> Query = this.dbSet.Include(m => m.Items);

            List<string> searchAttributes = new List<string>()
            {
                "Code"
            };

            Query = QueryHelper<TransferInDoc>.ConfigureSearch(Query, searchAttributes, Keyword);

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            Query = QueryHelper<TransferInDoc>.ConfigureFilter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = QueryHelper<TransferInDoc>.ConfigureOrder(Query, OrderDictionary);

            Pageable<TransferInDoc> pageable = new Pageable<TransferInDoc>(Query, Page - 1, Size);
            List<TransferInDoc> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }
        public TransferInDoc ReadById(int id)
        {
            var model = dbSet.Where(m => m.Id == id)
                 .Include(m => m.Items)
                 .FirstOrDefault();
            return model;
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
        public async Task<int> Create(TransferInDoc model, string username, int clientTimeZoneOffset = 7)
        {
            int Created = 0;

            using (var transaction = this.dbContext.Database.BeginTransaction())
            {
                try
                {
                    string code = GenerateCode("MM-TB/BBP");
                    model.Code = code;
                  
                    
                    var SPK = dbContext.SPKDocs.Where(x => x.PackingList == model.Reference).Single();
                    SPK.IsReceived = true;
                    var Id = SPK.Id;
                    EntityExtension.FlagForCreate(model, username, USER_AGENT);
                    foreach (var i in model.Items)
                    {
                        i.Id = 0;
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                        var SPKItems = dbContext.SPKDocsItems.Where(x => x.ItemArticleRealizationOrder == i.ArticleRealizationOrder && x.ItemCode == i.ItemCode && i.ItemName == i.ItemName && x.SPKDocsId == Id).Single();
                        SPKItems.SendQuantity = i.Quantity;
                        var inventorymovement = new InventoryMovement();
                        var inven = dbContext.Inventories.Where(x => x.ItemId == i.ItemId && x.StorageId == model.DestinationId).FirstOrDefault();
                        if (inven != null)
                        {
                            inventorymovement.Before = inven.Quantity;
                            inven.Quantity = inven.Quantity + i.Quantity ;//inven.Quantity + i.quantity;
                                                                                                                              //dbSetInventory.Update(inven);
                        }
                        else
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
                                Quantity = i.Quantity,
                                StorageCode = model.DestinationCode,
                                StorageId = model.DestinationId,
                                StorageName = model.DestinationName,
                                StorageIsCentral = model.DestinationName.Contains("GUDANG") ? true : false,
                            };
                            EntityExtension.FlagForCreate(inventory, username, USER_AGENT);
                            dbSetInventory.Add(inventory);
                        }

                        inventorymovement.After = inventorymovement.Before + i.Quantity;
                        inventorymovement.Date = DateTimeOffset.UtcNow;
                        inventorymovement.ItemCode = i.ItemCode;
                        inventorymovement.ItemDomesticCOGS = i.DomesticCOGS;
                        inventorymovement.ItemDomesticRetail = i.DomesticRetail;
                        inventorymovement.ItemDomesticWholeSale = i.DomesticRetail;
                        inventorymovement.ItemDomesticSale = i.DomesticSale;
                        inventorymovement.ItemId = i.ItemId;
                        inventorymovement.ItemInternationalCOGS = 0;
                        inventorymovement.ItemInternationalRetail = 0;
                        inventorymovement.ItemInternationalSale = 0;
                        inventorymovement.ItemInternationalWholeSale = 0;
                        inventorymovement.ItemName = i.ItemName;
                        inventorymovement.ItemSize = i.Size;
                        inventorymovement.ItemUom = i.Uom;
                        inventorymovement.Quantity = i.Quantity;
                        inventorymovement.StorageCode = model.DestinationCode;
                        inventorymovement.StorageId = model.DestinationId;
                        inventorymovement.StorageName = model.DestinationName;
                        inventorymovement.Type = "IN";
                        inventorymovement.Reference = code;
                        inventorymovement.Remark = model.Remark;
                        inventorymovement.StorageIsCentral = model.DestinationName.Contains("GUDANG") ? true : false;
                        EntityExtension.FlagForCreate(inventorymovement, username, USER_AGENT);
                        dbSetInventoryMovement.Add(inventorymovement);

                    }

                    dbSet.Add(model);
                    Created = await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }
            }

            return Created;
        }

        public async Task<int> CreateForPos(TransferInDoc model, string username, int clientTimeZoneOffset = 7)
        {
            int Created = 0;

            using (var transaction = this.dbContext.Database.BeginTransaction())
            {
                try
                {

                    EntityExtension.FlagForCreate(model, username, USER_AGENT);
                    foreach (var i in model.Items)
                    {
                        i.Id = 0;
                        EntityExtension.FlagForCreate(i, username, USER_AGENT);
                        var inventorymovement = new InventoryMovement();
                        var inven = dbContext.Inventories.Where(x => x.ItemId == i.ItemId && x.StorageId == model.DestinationId).FirstOrDefault();
                        if (inven != null)
                        {
                            inventorymovement.Before = inven.Quantity;
                            inven.Quantity = inven.Quantity + i.Quantity;//inven.Quantity + i.quantity;
                                                                         //dbSetInventory.Update(inven);
                        }
                        else
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
                                Quantity = i.Quantity,
                                StorageCode = model.DestinationCode,
                                StorageId = model.DestinationId,
                                StorageName = model.DestinationName,
                                StorageIsCentral = model.DestinationName.Contains("GUDANG") ? true : false,
                            };
                            EntityExtension.FlagForCreate(inventory, username, USER_AGENT);
                            dbSetInventory.Add(inventory);
                        }

                        inventorymovement.After = inventorymovement.Before + i.Quantity;
                        inventorymovement.Date = DateTimeOffset.UtcNow;
                        inventorymovement.ItemCode = i.ItemCode;
                        inventorymovement.ItemDomesticCOGS = i.DomesticCOGS;
                        inventorymovement.ItemDomesticRetail = i.DomesticRetail;
                        inventorymovement.ItemDomesticWholeSale = i.DomesticRetail;
                        inventorymovement.ItemDomesticSale = i.DomesticSale;
                        inventorymovement.ItemId = i.ItemId;
                        inventorymovement.ItemInternationalCOGS = 0;
                        inventorymovement.ItemInternationalRetail = 0;
                        inventorymovement.ItemInternationalSale = 0;
                        inventorymovement.ItemInternationalWholeSale = 0;
                        inventorymovement.ItemName = i.ItemName;
                        inventorymovement.ItemSize = i.Size;
                        inventorymovement.ItemUom = i.Uom;
                        inventorymovement.Quantity = i.Quantity;
                        inventorymovement.StorageCode = model.DestinationCode;
                        inventorymovement.StorageId = model.DestinationId;
                        inventorymovement.StorageName = model.DestinationName;
                        inventorymovement.Type = "IN";
                        inventorymovement.Reference = model.Code;
                        inventorymovement.Remark = model.Remark;
                        inventorymovement.StorageIsCentral = model.DestinationName.Contains("GUDANG") ? true : false;
                        EntityExtension.FlagForCreate(inventorymovement, username, USER_AGENT);
                        dbSetInventoryMovement.Add(inventorymovement);

                    }

                    dbSet.Add(model);
                    Created = await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }
            }

            return Created;
        }

        //public async Task<int> Create(SPKDocsViewModel model, string username, int clientTimeZoneOffset = 7)
        //{
        //    int Created = 0;

        //    using (var transaction = this.dbContext.Database.BeginTransaction())
        //    {
        //        try
        //        {
        //            List<TransferInDocItemViewModel> transferInDocItemViewModels = new List<TransferInDocItemViewModel>();
        //            foreach (var i in model.items)
        //            {
        //                var inventorymovement = new InventoryMovement();
        //                var inven = dbContext.Inventories.Where(x => x.ItemId == i.item._id && x.StorageId == model.destination._id).FirstOrDefault();
        //                if (inven != null)
        //                {
        //                    inventorymovement.Before = inven.Quantity;
        //                    inven.Quantity = (model.type == "IN" ? inven.Quantity + i.quantity : inven.Quantity - i.quantity);//inven.Quantity + i.quantity;
        //                    //dbSetInventory.Update(inven);
        //                }
        //                else
        //                {
        //                    Inventory inventory = new Inventory{
        //                        ItemArticleRealizationOrder = i.item.articleRealizationOrder,
        //                        ItemCode = i.item.code,
        //                        ItemDomesticCOGS = i.item.domesticCOGS,
        //                        ItemDomesticRetail = i.item.domesticRetail,
        //                        ItemDomesticSale = i.item.domesticSale,
        //                        itemDomesticWholeSale = i.item.domesticWholesale,
        //                        ItemId = i.item._id,
        //                        ItemInternationalCOGS = 0,
        //                        ItemInternationalRetail = 0,
        //                        ItemInternationalSale = 0,
        //                        ItemInternationalWholeSale = 0,
        //                        ItemName = i.item.name,
        //                        ItemSize = i.item.size,
        //                        ItemUom = i.item.uom,
        //                        Quantity = i.quantity,
        //                        StorageCode = model.destination.code,
        //                        StorageId = model.destination._id,
        //                        StorageName = model.destination.name,
        //                        StorageIsCentral = model.destination.name.Contains("GUDANG") ? true : false
        //                    };
        //                    dbSetInventory.Add(inventory);
        //                }
        //                inventorymovement.After = inventorymovement.Before + i.quantity;
        //                inventorymovement.Date = DateTimeOffset.UtcNow;
        //                inventorymovement.ItemCode = i.item.code;
        //                inventorymovement.ItemDomesticCOGS = i.item.domesticCOGS;
        //                inventorymovement.ItemDomesticRetail = i.item.domesticRetail;
        //                inventorymovement.ItemDomesticWholeSale = i.item.domesticWholesale;
        //                inventorymovement.ItemDomesticSale = i.item.domesticSale;
        //                inventorymovement.ItemId = i.item._id;
        //                inventorymovement.ItemInternationalCOGS = 0;
        //                inventorymovement.ItemInternationalRetail = 0;
        //                inventorymovement.ItemInternationalSale = 0;
        //                inventorymovement.ItemInternationalWholeSale = 0;
        //                inventorymovement.ItemName = i.item.name;
        //                inventorymovement.ItemSize = i.item.size;
        //                inventorymovement.ItemUom = i.item.uom;
        //                inventorymovement.Quantity = i.quantity;
        //                inventorymovement.StorageCode = model.destination.code;
        //                inventorymovement.StorageId = model.destination._id;
        //                inventorymovement.StorageName = model.destination.name;
        //                inventorymovement.Type = model.type;
        //                inventorymovement.StorageIsCentral = model.destination.name.Contains("GUDANG") ? true : false;
        //                dbSetInventoryMovement.Add(inventorymovement);

        //                transferInDocItemViewModels.Add(new TransferInDocItemViewModel
        //                {
        //                    articleRealizationOrder = i.item.articleRealizationOrder,
        //                    item = new ViewModels.NewIntegrationViewModel.ItemViewModel
        //                    {
        //                        articleRealizationOrder = i.item.articleRealizationOrder,
        //                        code = i.item.code,
        //                        domesticCOGS = i.item.domesticCOGS,
        //                        domesticRetail = i.item.domesticRetail,
        //                        domesticSale = i.item.domesticSale,
        //                        domesticWholesale = i.item.domesticWholesale,
        //                        name = i.item.name,
        //                        size = i.item.size,
        //                        uom = i.item.uom,
        //                        _id = i.item._id
        //                    },
        //                    quantity = i.quantity,
        //                    remark = i.remark,
        //                    _id = i._id

        //                });



        //            }
        //            TransferInDoc transferInDoc = new TransferInDoc
        //            {
        //                Code = model.PackingList,
        //                Date = DateTimeOffset.UtcNow,
        //                DestinationCode = model.destination.code,
        //                DestinationId = model.destination._id,
        //                DestinationName = model.destination.name,
        //                Reference = ,
        //                a

        //            }


        //            double _total = 0;
        //            EntityExtension.FlagForCreate(model, username, USER_AGENT);
        //            foreach (var item in model.Items)
        //            {
        //                _total += item.TotalAmount;
        //                GarmentDeliveryOrder deliveryOrder = dbSetDeliveryOrder.FirstOrDefault(s => s.Id == item.DeliveryOrderId);
        //                if (deliveryOrder != null)
        //                    deliveryOrder.IsInvoice = true;
        //                EntityExtension.FlagForCreate(item, username, USER_AGENT);

        //                foreach (var detail in item.Details)
        //                {
        //                    EntityExtension.FlagForCreate(detail, username, USER_AGENT);
        //                }
        //            }
        //            model.TotalAmount = _total;

        //            this.dbSet.Add(model);
        //            Created = await dbContext.SaveChangesAsync();
        //            transaction.Commit();
        //        }
        //        catch (Exception e)
        //        {
        //            transaction.Rollback();
        //            throw new Exception(e.Message);
        //        }
        //    }

        //    return Created;
        //}



    }
}
