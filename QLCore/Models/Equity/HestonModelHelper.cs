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
   //! calibration helper for Heston model
   public class HestonModelHelper : CalibrationHelper
   {
      public HestonModelHelper(Period maturity,
                               Calendar calendar,
                               double s0,
                               double strikePrice,
                               Handle<Quote> volatility,
                               Handle<YieldTermStructure> riskFreeRate,
                               Handle<YieldTermStructure> dividendYield,
                               CalibrationHelper.CalibrationErrorType errorType = CalibrationErrorType.RelativePriceError)
         : base(volatility, riskFreeRate, errorType)
      {
         maturity_ = maturity;
         calendar_ = calendar;
         s0_ = new Handle<Quote>(new SimpleQuote(s0));
         strikePrice_ = strikePrice;
         dividendYield_ = dividendYield;
      }

      public HestonModelHelper(Period maturity,
                               Calendar calendar,
                               Handle<Quote> s0,
                               double strikePrice,
                               Handle<Quote> volatility,
                               Handle<YieldTermStructure> riskFreeRate,
                               Handle<YieldTermStructure> dividendYield,
                               CalibrationHelper.CalibrationErrorType errorType = CalibrationErrorType.RelativePriceError)
         : base(volatility, riskFreeRate, errorType)
      {
         maturity_ = maturity;
         calendar_ = calendar;
         s0_ = s0;
         strikePrice_ = strikePrice;
         dividendYield_ = dividendYield;
      }

      public override void addTimesTo(List<double> t) {}

      protected override void performCalculations()
      {
         exerciseDate_ = calendar_.advance(termStructure_.link.referenceDate(), maturity_);
         tau_ = termStructure_.link.timeFromReference(exerciseDate_);
         type_ = strikePrice_ * termStructure_.link.discount(tau_) >=
                 s0_.link.value() * dividendYield_.link.discount(tau_)
                 ? Option.Type.Call
                 : Option.Type.Put;
         StrikedTypePayoff payoff = new PlainVanillaPayoff(type_, strikePrice_);
         Exercise exercise = new EuropeanExercise(exerciseDate_);
         option_ = new VanillaOption(payoff, exercise);
         base.performCalculations();
      }

      public override double modelValue()
      {
         calculate();
         option_.setPricingEngine(engine_);
         return option_.NPV();
      }

      public override double blackPrice(double volatility)
      {
         calculate();
         double stdDev = volatility * Math.Sqrt(maturity());
         return Utils.blackFormula(type_, strikePrice_ * termStructure_.link.discount(tau_),
                                   s0_.link.value() * dividendYield_.link.discount(tau_), stdDev);
      }

      public double maturity()  { calculate(); return tau_; }
      public Option.Type optionType() { calculate(); return type_; }
      public double strike() { return strikePrice_; }

      private Period maturity_;
      private Calendar calendar_;
      private Handle<Quote> s0_;
      private double strikePrice_;
      private Handle<YieldTermStructure> dividendYield_;
      private Date exerciseDate_;
      private double tau_;
      private Option.Type type_;
      private VanillaOption option_;
   }


}
