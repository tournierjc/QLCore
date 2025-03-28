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
   //! Abstract base forward class
   /*! Derived classes must implement the virtual functions spotValue() (NPV or spot price) and spotIncome() associated
      with the specific relevant underlying (e.g. bond, stock, commodity, loan/deposit). These functions must be used to set the
      protected member variables underlyingSpotValue_ and underlyingIncome_ within performCalculations() in the derived
      class before the base-class implementation is called.

      spotIncome() refers generically to the present value of coupons, dividends or storage costs.

      discountCurve_ is the curve used to discount forward contract cash flows back to the evaluation day, as well as to obtain
      forward values for spot values/prices.

      incomeDiscountCurve_, which for generality is not automatically set to the discountCurve_, is the curve used to
      discount future income/dividends/storage-costs etc back to the evaluation date.

      \todo Add preconditions and tests

      \warning This class still needs to be rigorously tested

      \ingroup instruments
   */
   public abstract class Forward : Instrument
   {
      /*! derived classes must set this, typically via spotIncome() */
      protected double underlyingIncome_;
      /*! derived classes must set this, typically via spotValue() */
      protected double underlyingSpotValue_;

      protected DayCounter dayCounter_;
      protected Calendar calendar_;
      protected BusinessDayConvention businessDayConvention_;
      protected int settlementDays_;
      protected Payoff payoff_;
      /*! valueDate = settlement date (date the fwd contract starts accruing) */
      protected Date valueDate_;
      //! maturityDate of the forward contract or delivery date of underlying
      protected Date maturityDate_;
      protected Handle<YieldTermStructure> discountCurve_;
      /*! must set this in derived classes, based on particular underlying */
      protected Handle<YieldTermStructure> incomeDiscountCurve_;

      protected Forward(DayCounter dayCounter, Calendar calendar, BusinessDayConvention businessDayConvention,
                        int settlementDays, Payoff payoff, Date valueDate, Date maturityDate,
                        Handle<YieldTermStructure> discountCurve)
      {
         dayCounter_ = dayCounter;
         calendar_ = calendar;
         businessDayConvention_ = businessDayConvention;
         settlementDays_ = settlementDays;
         payoff_ = payoff;
         valueDate_ = valueDate;
         maturityDate_ = maturityDate;
         discountCurve_ = discountCurve;

         maturityDate_ = calendar_.adjust(maturityDate_, businessDayConvention_);
      }

      public virtual Date settlementDate()
      {
         Date d = calendar_.advance(Settings.Instance.evaluationDate(), settlementDays_, TimeUnit.Days);
         return Date.Max(d, valueDate_);
      }

      public override bool isExpired()
      {
         return new simple_event(maturityDate_).hasOccurred(settlementDate());
      }


      //! returns spot value/price of an underlying financial instrument
      public abstract double spotValue();
      //! NPV of income/dividends/storage-costs etc. of underlying instrument
      public abstract double spotIncome(Handle<YieldTermStructure> incomeDiscountCurve);

      // Calculations
      //! forward value/price of underlying, discounting income/dividends
      /*! \note if this is a bond forward price, is must be a dirty
              forward price.
      */
      public virtual double forwardValue()
      {
         calculate();
         return (underlyingSpotValue_ - underlyingIncome_) / discountCurve_.link.discount(maturityDate_);
      }

      /*! Simple yield calculation based on underlying spot and
      forward values, taking into account underlying income.
      When \f$ t>0 \f$, call with:
      underlyingSpotValue=spotValue(t),
      forwardValue=strikePrice, to get current yield. For a
      repo, if \f$ t=0 \f$, impliedYield should reproduce the
      spot repo rate. For FRA's, this should reproduce the
      relevant zero rate at the FRA's maturityDate_
      */
      public InterestRate impliedYield(double underlyingSpotValue, double forwardValue, Date settlementDate,
                                       Compounding compoundingConvention, DayCounter dayCounter)
      {

         double tenor = dayCounter.yearFraction(settlementDate, maturityDate_) ;
         double compoundingFactor = forwardValue / (underlyingSpotValue - spotIncome(incomeDiscountCurve_)) ;
         return InterestRate.impliedRate(compoundingFactor, dayCounter, compoundingConvention, Frequency.Annual, tenor);
      }

      protected override void performCalculations()
      {
         Utils.QL_REQUIRE(!discountCurve_.empty(), () => "no discounting term structure set to Forward");

         ForwardTypePayoff ftpayoff = payoff_ as ForwardTypePayoff;
         double fwdValue = forwardValue();
         NPV_ = ftpayoff.value(fwdValue) * discountCurve_.link.discount(maturityDate_);
      }
   }

   //! Class for forward type payoffs
   public class ForwardTypePayoff : Payoff
   {
      protected Position.Type type_;
      public Position.Type forwardType() { return type_; }

      protected double strike_;
      public double strike() { return strike_; }

      public ForwardTypePayoff(Position.Type type, double strike)
      {
         type_ = type;
         strike_ = strike;
         Utils.QL_REQUIRE(strike >= 0.0, () => "negative strike given");
      }

      // Payoff interface
      public override string name() { return "Forward";}
      public override string description()
      {
         string result = name() + ", " + strike() + " strike";
         return result;
      }
      public override double value(double price)
      {
         switch (type_)
         {
            case Position.Type.Long:
               return (price - strike_);
            case Position.Type.Short:
               return (strike_ - price);
            default:
               Utils.QL_FAIL("unknown/illegal position type");
               return 0;
         }
      }
   }
}
