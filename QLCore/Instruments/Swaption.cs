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
using System.Linq;

namespace QLCore
{
   //! %settlement information
   public struct Settlement
   {
      public enum Type { Physical, Cash };
      public enum Method { PhysicalOTC,
                           PhysicalCleared,
                           CollateralizedCashPrice,
                           ParYieldCurve
                         };

      public static void checkTypeAndMethodConsistency(Type settlementType, Method settlementMethod)
      {
         if (settlementType == Type.Physical)
         {
            Utils.QL_REQUIRE(settlementMethod == Method.PhysicalOTC ||
                             settlementMethod == Method.PhysicalCleared,
                             () => "invalid settlement method for physical settlement");
         }
         if (settlementType == Type.Cash)
         {
            Utils.QL_REQUIRE(settlementMethod == Method.CollateralizedCashPrice ||
                             settlementMethod == Method.ParYieldCurve,
                             () => "invalid settlement method for cash settlement");
         }
      }
   }

   //! %Swaption class
   /*! \ingroup instruments

       \test
       - the correctness of the returned value is tested by checking
         that the price of a payer (resp. receiver) swaption
         decreases (resp. increases) with the strike.
       - the correctness of the returned value is tested by checking
         that the price of a payer (resp. receiver) swaption
         increases (resp. decreases) with the spread.
       - the correctness of the returned value is tested by checking
         it against that of a swaption on a swap with no spread and a
         correspondingly adjusted fixed rate.
       - the correctness of the returned value is tested by checking
         it against a known good value.
       - the correctness of the returned value of cash settled swaptions
         is tested by checking the modified annuity against a value
         calculated without using the Swaption class.


       \todo add greeks and explicit exercise lag
   */

   public class Swaption : Option
   {

      public Arguments arguments { get; set; }
      public SwaptionEngine engine { get; set; }

      // arguments
      private VanillaSwap swap_;
      private Settlement.Type settlementType_;
      private Settlement.Method settlementMethod_;

      public Swaption(VanillaSwap swap,
                      Exercise exercise,
                      Settlement.Type delivery = Settlement.Type.Physical,
                      Settlement.Method settlementMethod = Settlement.Method.PhysicalOTC)
         : base(new Payoff(), exercise)
      {
         settlementType_ = delivery;
         settlementMethod_ = settlementMethod;
         swap_ = swap;
      }

      // Instrument interface
      public override bool isExpired()
      {
         return new simple_event(exercise_.dates().Last()).hasOccurred();
      }

      public override void setupArguments(IPricingEngineArguments args)
      {
         swap_.setupArguments(args);

         Swaption.Arguments arguments = args as Swaption.Arguments;
         if (arguments == null)
            throw new ArgumentException("wrong argument type");
         arguments.swap = swap_;
         arguments.settlementType = settlementType_;
         arguments.settlementMethod = settlementMethod_;
         arguments.exercise = exercise_;
      }

      public void validate()
      {
         arguments.validate();
         if (arguments.swap == null)
            throw new ArgumentException("vanilla swap not set");
         if (arguments.exercise == null)
            throw new ArgumentException("exercise not set");
         Settlement.checkTypeAndMethodConsistency(arguments.settlementType,
                                                  arguments.settlementMethod);
      }

      // Inspectors
      public Settlement.Type settlementType()
      {
         return settlementType_;
      }

      public Settlement.Method settlementMethod()
      {
         return settlementMethod_;
      }

      public VanillaSwap.Type type()
      {
         return swap_.swapType;
      }

      public VanillaSwap underlyingSwap()
      {
         return swap_;
      }

      //! implied volatility
      public double impliedVolatility(double targetValue,
                                      Handle<YieldTermStructure> discountCurve,
                                      double guess,
                                      double accuracy = 1.0e-4,
                                      int maxEvaluations = 100,
                                      double minVol = 1.0e-7,
                                      double maxVol = 4.0,
                                      VolatilityType type = VolatilityType.ShiftedLognormal,
                                      double? displacement = 0.0)
      {
         calculate();
         if (isExpired())
            throw new ArgumentException("instrument expired");
         ImpliedVolHelper_ f = new ImpliedVolHelper_(this, discountCurve, targetValue, displacement, type);
         NewtonSafe solver = new NewtonSafe();
         solver.setMaxEvaluations(maxEvaluations);
         return solver.solve(f, accuracy, guess, minVol, maxVol);
      }

      // arguments, pricing engine
      public new class Arguments : VanillaSwap.Arguments
      {
         public Exercise exercise { get; set; }
         public VanillaSwap swap { get; set; }
         public Settlement.Type settlementType { get; set; }
         public Settlement.Method settlementMethod { get; set; }
         public Arguments()
         {
            settlementType = Settlement.Type.Physical;
         }
      }
   }

   //! base class for swaption engines
   public abstract class SwaptionEngine : GenericEngine<Swaption.Arguments, Swaption.Results> { }

   public class ImpliedVolHelper_ : ISolver1d
   {

      private IPricingEngine engine_;
      private Handle<YieldTermStructure> discountCurve_;
      private double targetValue_;
      private SimpleQuote vol_;
      private Instrument.Results results_;

      public ImpliedVolHelper_(Swaption swaption,
                               Handle<YieldTermStructure> discountCurve,
                               double targetValue,
                               double? displacement = 0.0,
                               VolatilityType type = VolatilityType.ShiftedLognormal)
      {
         discountCurve_ = discountCurve;
         targetValue_ = targetValue;
         // set an implausible value, so that calculation is forced
         // at first ImpliedVolHelper::operator()(Volatility x) call
         vol_ = new SimpleQuote(-1.0);
         Handle<Quote> h = new Handle<Quote>(vol_);
         switch (type)
         {
            case VolatilityType.ShiftedLognormal:
               engine_ = new BlackSwaptionEngine(discountCurve_, h, new Actual365Fixed(), displacement);
               break;
            case VolatilityType.Normal:
               engine_ = new BachelierSwaptionEngine(discountCurve_, h, new Actual365Fixed());
               break;
            default:
               Utils.QL_FAIL("unknown VolatilityType (" + type.ToString() + ")");
               break;
         }
         swaption.setupArguments(engine_.getArguments());
         results_ = engine_.getResults() as Instrument.Results;
      }

      public override double value(double x)
      {
         if (x.IsNotEqual(vol_.value()))
         {
            vol_.setValue(x);
            engine_.calculate();
         }
         return results_.value.Value - targetValue_;
      }

      public override double derivative(double x)
      {
         if (x.IsNotEqual(vol_.value()))
         {
            vol_.setValue(x);
            engine_.calculate();
         }
         Utils.QL_REQUIRE(results_.additionalResults.Keys.Contains("vega"), () => "vega not provided");
         return (double)results_.additionalResults["vega"];
      }
   }
}



