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

using System;
using System.Collections.Generic;

namespace QLCore
{
   //! swap paying Libor against BMA coupons
   public class BMASwap : Swap
   {
      public enum Type { Receiver = -1, Payer = 1 }

      private Type type_;
      public Type type() { return type_; }

      private double nominal_;
      public double nominal() { return nominal_; }

      private double liborFraction_;
      public double liborFraction() { return liborFraction_; }

      private double liborSpread_;
      public double liborSpread() { return liborSpread_; }


      public BMASwap(Type type, double nominal,
                     // Libor leg
                     Schedule liborSchedule, double liborFraction, double liborSpread, IborIndex liborIndex, DayCounter liborDayCount,
                     // BMA leg
                     Schedule bmaSchedule, BMAIndex bmaIndex, DayCounter bmaDayCount)
         : base(2)
      {
         type_ = type;
         nominal_ = nominal;
         liborFraction_ = liborFraction;
         liborSpread_ = liborSpread;

         BusinessDayConvention convention = liborSchedule.businessDayConvention();

         legs_[0] = new IborLeg(liborSchedule, liborIndex)
         .withPaymentDayCounter(liborDayCount)
         .withFixingDays(liborIndex.fixingDays())
         .withGearings(liborFraction)
         .withSpreads(liborSpread)
         .withNotionals(nominal)
         .withPaymentAdjustment(convention);

         legs_[1] = new AverageBMALeg(bmaSchedule, bmaIndex)
         .withPaymentDayCounter(bmaDayCount)
         .withNotionals(nominal)
         .withPaymentAdjustment(bmaSchedule.businessDayConvention());

         switch (type_)
         {
            case Type.Payer:
               payer_[0] = +1.0;
               payer_[1] = -1.0;
               break;
            case Type.Receiver:
               payer_[0] = -1.0;
               payer_[1] = +1.0;
               break;
            default:
               Utils.QL_FAIL("Unknown BMA-swap type");
               break;
         }
      }


      public List<CashFlow> liborLeg() { return legs_[0]; }
      public List<CashFlow> bmaLeg() { return legs_[1]; }

      public double liborLegBPS()
      {
         calculate();
         Utils.QL_REQUIRE(legBPS_[0] != null, () => "result not available");
         return legBPS_[0].GetValueOrDefault();
      }

      public double liborLegNPV()
      {
         calculate();
         Utils.QL_REQUIRE(legNPV_[0] != null, () => "result not available");
         return legNPV_[0].GetValueOrDefault();
      }

      public double fairLiborFraction()
      {
         double spreadNPV = (liborSpread_ / Const.BASIS_POINT) * liborLegBPS();
         double pureLiborNPV = liborLegNPV() - spreadNPV;
         Utils.QL_REQUIRE(pureLiborNPV.IsNotEqual(0.0), () => "result not available (null libor NPV)");
         return -liborFraction_ * (bmaLegNPV() + spreadNPV) / pureLiborNPV;
      }

      public double fairLiborSpread()
      {
         return liborSpread_ - NPV() / (liborLegBPS() / Const.BASIS_POINT);
      }

      public double bmaLegBPS()
      {
         calculate();
         Utils.QL_REQUIRE(legBPS_[1] != null, () => "result not available");
         return legBPS_[1].GetValueOrDefault();
      }

      public double bmaLegNPV()
      {
         calculate();
         Utils.QL_REQUIRE(legNPV_[1] != null, () => "result not available");
         return legNPV_[1].GetValueOrDefault();
      }
   }
}
