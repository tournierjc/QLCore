﻿/*
 Copyright (C) 2020 Jean-Camille Tournier (mail@tournierjc.fr)

 This file is part of QLCore Project https://github.com/OpenDerivatives/QLCore

 QLCore is free software: you can redistribute it and/or modify it
 under the terms of the QLCore and QLNet license. You should have received a
 copy of the license along with this program; if not, license is
 available at https://github.com/OpenDerivatives/QLCore/LICENSE.

 QLCore is a forked of QLNet which is a based on QuantLib, a free-software/open-source
 library for financial quantitative analysts and developers - http://quantlib.org/
 The QuantLib license is available online at http://quantlib.org/license.shtml and the
 QLNet license is available online at https://github.com/amaggiulli/QLNet/blob/develop/LICENSE.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICAR PURPOSE. See the license for more details.
*/

namespace QLCore
{
   public class MidPointCdsEngine : CreditDefaultSwap.Engine
   {
      public MidPointCdsEngine(Handle<DefaultProbabilityTermStructure> probability,
                               double recoveryRate,
                               Handle<YieldTermStructure> discountCurve,
                               bool? includeSettlementDateFlows = null)
      {
         probability_ = probability;
         recoveryRate_ = recoveryRate;
         discountCurve_ = discountCurve;
         includeSettlementDateFlows_ = includeSettlementDateFlows;
      }

      public override void calculate()
      {
         Utils.QL_REQUIRE(!discountCurve_.empty(), () => "no discount term structure set");
         Utils.QL_REQUIRE(!probability_.empty(), () => "no probability term structure set");

         Date today = Settings.Instance.evaluationDate();
         Date settlementDate = discountCurve_.link.referenceDate();

         // Upfront Flow NPV. Either we are on-the-run (no flow)
         // or we are forward start

         // date determining the probability survival so we have to pay
         // the upfront (did not knock out)
         Date effectiveProtectionStart =  arguments_.protectionStart > probability_.link.referenceDate() ?
                                             arguments_.protectionStart : probability_.link.referenceDate();

         double nonKnockOut = probability_.currentLink().survivalProbability(effectiveProtectionStart);

         double upfPVO1 = 0.0;
         results_.upfrontNPV = 0.0;
         if (!arguments_.upfrontPayment.hasOccurred(settlementDate, includeSettlementDateFlows_))
         {
            upfPVO1 = nonKnockOut  * discountCurve_.link.discount(arguments_.upfrontPayment.date());

            if(arguments_.upfrontPayment.amount() != 0) 
            {
                results_.upfrontNPV = upfPVO1 * arguments_.upfrontPayment.amount();
            }
         }

         results_.accrualRebateNPV = 0.0;
         if (arguments_.accrualRebate != null && 
             arguments_.accrualRebate.amount() != 0 &&
            !arguments_.accrualRebate.hasOccurred(settlementDate, includeSettlementDateFlows_)) 
         {
            results_.accrualRebateNPV = nonKnockOut *
                  discountCurve_.link.discount(arguments_.accrualRebate.date()) *
                     arguments_.accrualRebate.amount();
         }

         results_.couponLegNPV  = 0.0;
         results_.defaultLegNPV = 0.0;
         for (int i = 0; i < arguments_.leg.Count; ++i)
         {
            if (arguments_.leg[i].hasOccurred(settlementDate, includeSettlementDateFlows_))
               continue;

            FixedRateCoupon coupon = arguments_.leg[i] as FixedRateCoupon;

            // In order to avoid a few switches, we calculate the NPV
            // of both legs as a positive quantity. We'll give them
            // the right sign at the end.

            Date paymentDate = coupon.date(),
                 startDate = coupon.accrualStartDate(),
                 endDate = coupon.accrualEndDate();
            // this is the only point where it might not coincide
            if (i == 0)
               startDate = arguments_.protectionStart;
            Date effectiveStartDate =
               (startDate <= today && today <= endDate) ? today : startDate;
            Date defaultDate = // mid-point
               effectiveStartDate + (endDate - effectiveStartDate) / 2;

            double S = probability_.link.survivalProbability(paymentDate);
            double P = probability_.link.defaultProbability(effectiveStartDate, endDate);

            // on one side, we add the fixed rate payments in case of
            // survival...
            results_.couponLegNPV +=
               S * coupon.amount() *
               discountCurve_.link.discount(paymentDate);
            // ...possibly including accrual in case of default.
            if (arguments_.settlesAccrual)
            {
               if (arguments_.paysAtDefaultTime)
               {
                  results_.couponLegNPV +=
                     P * coupon.accruedAmount(defaultDate) *
                     discountCurve_.link.discount(defaultDate);
               }
               else
               {
                  // pays at the end
                  results_.couponLegNPV +=
                     P * coupon.amount() *
                     discountCurve_.link.discount(paymentDate);
               }
            }

            // on the other side, we add the payment in case of default.
            double claim = arguments_.claim.amount(defaultDate,
                                                   arguments_.notional.Value,
                                                   recoveryRate_);
            if (arguments_.paysAtDefaultTime)
            {
               results_.defaultLegNPV +=
                  P * claim * discountCurve_.link.discount(defaultDate);
            }
            else
            {
               results_.defaultLegNPV +=
                  P * claim * discountCurve_.link.discount(paymentDate);
            }
         }

         double upfrontSign = 1.0;
         switch (arguments_.side)
         {
            case CreditDefaultSwap.Protection.Side.Seller:
               results_.defaultLegNPV *= -1.0;
               results_.accrualRebateNPV *= -1.0;
               break;
            case CreditDefaultSwap.Protection.Side.Buyer:
               results_.couponLegNPV *= -1.0;
               results_.upfrontNPV   *= -1.0;
               upfrontSign = -1.0;
               break;
            default:
               Utils.QL_FAIL("unknown protection side");
               break;
         }

         results_.value = results_.defaultLegNPV + results_.couponLegNPV + 
                          results_.upfrontNPV + results_.accrualRebateNPV;
         results_.errorEstimate = null;

         if (results_.couponLegNPV.IsNotEqual(0.0))
         {
            results_.fairSpread =
               -results_.defaultLegNPV * arguments_.spread / 
                  (results_.couponLegNPV + results_.accrualRebateNPV);
         }
         else
         {
            results_.fairSpread = null;
         }

         double upfrontSensitivity = upfPVO1 * arguments_.notional.Value;
         if (upfrontSensitivity.IsNotEqual(0.0))
         {
            results_.fairUpfront =
               -upfrontSign * (results_.defaultLegNPV + results_.couponLegNPV + 
                  results_.accrualRebateNPV)
               / upfrontSensitivity;
         }
         else
         {
            results_.fairUpfront = null;
         }


         if (arguments_.spread.IsNotEqual(0.0))
         {
            results_.couponLegBPS =
               results_.couponLegNPV * Const.BASIS_POINT / arguments_.spread.Value;
         }
         else
         {
            results_.couponLegBPS = null;
         }

         if (arguments_.upfront.HasValue && arguments_.upfront.IsNotEqual(0.0))
         {
            results_.upfrontBPS =
               results_.upfrontNPV * Const.BASIS_POINT / (arguments_.upfront.Value);
         }
         else
         {
            results_.upfrontBPS = null;
         }
      }



      private Handle<DefaultProbabilityTermStructure> probability_;
      private double recoveryRate_;
      private Handle<YieldTermStructure> discountCurve_;
      private bool? includeSettlementDateFlows_;
   }
}
