using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Api.Infra.Resources.Payment;
using Cashflow.Api.Infra.Entity;
using Cashflow.Api.Service;
using Cashflow.Api.Shared;

namespace Cashflow.Api.Infra.Repository
{
    public class PaymentRepository : BaseRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(DatabaseContext conn, LogService logService) : base(conn, logService) { }

        public async Task<IEnumerable<PaymentType>> GetTypes()
        {
            var list = new List<Payment>();
            return await Query<PaymentType>(PaymentResources.Types);
        }

        public async Task<IEnumerable<Payment>> GetByUser(int userId)
        {
            var list = new List<Payment>();
            var types = await GetTypes();
            var result = await Query<Installment>(PaymentResources.ByUser, (p, i) =>
            {
                var pay = list.FirstOrDefault(x => x.Id == p.Id);
                if (pay == null)
                {
                    pay = p;
                    list.Add(p);
                    pay.Type = types.FirstOrDefault(t => t.Id == (int)p.TypeId);
                    pay.Installments = new List<Installment>();
                }
                pay.Installments.Add(i);
                return p;
            }, new { UserId = userId });
            return list;
        }

        public async Task<Payment> GetById(int id)
        {
            Payment payment = null;
            var types = await GetTypes();
            var result = await Query<Installment>(PaymentResources.ById, (p, i) =>
            {
                if (payment == null)
                {
                    payment = p;
                    payment.Installments = new List<Installment>();
                    payment.Type = types.FirstOrDefault(t => t.Id == (int)p.TypeId);
                }
                payment.Installments.Add(i);
                return p;
            }, new { Id = id });
            return payment;
        }

        public Task<IEnumerable<Payment>> GetAll()
        {
            throw new NotImplementedException();
        }

        public async Task Add(Payment payment)
        {
            BeginTransaction();
            await Execute(PaymentResources.Insert, payment);
            var currentId = await NextId();
            foreach (var i in payment.Installments)
            {
                i.PaymentId = currentId;
                await Execute(InstallmentResources.Insert, i);
            }
        }

        public async Task Update(Payment payment)
        {
            BeginTransaction();
            var payDb = await GetById(payment.Id);
            await Execute(InstallmentResources.Delete, new { PaymentId = payment.Id });
            await Execute(PaymentResources.Update, payment);
            foreach (var i in payment.Installments.OrderBy(p => p.Number))
            {
                i.PaymentId = payment.Id;
                await Execute(InstallmentResources.Insert, i);
            }
            payDb = await GetById(payment.Id);
        }

        public async Task Remove(int id)
        {
            BeginTransaction();
            await Execute(InstallmentResources.Delete, new { PaymentId = id });
            await Execute(PaymentResources.Delete, new { Id = id });
        }

        public DateTime CurrentDate => DateTime.Now;
    }
}